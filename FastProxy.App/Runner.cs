using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FastProxy.Listeners;
using FastProxy.Listeners.Chaos;
using FastProxy.TestSupport;

namespace FastProxy.App
{
    public class Runner : IDisposable
    {
        public static void Run(Options options, LoadTestOptions verbOptions, Func<IPEndPoint, FastServer> serverFactory, Func<IPEndPoint, FastClient> clientFactory)
        {
            using (var runner = new Runner(options, verbOptions, serverFactory, clientFactory))
            {
                runner.Run();
            }
        }

        private readonly int parallel;
        private readonly int count;
        private readonly Options options;
        private readonly Func<IPEndPoint, FastServer> serverFactory;
        private readonly Func<IPEndPoint, FastClient> clientFactory;
        private readonly BandwidthListener listener = new BandwidthListener(SinkListener.Instance);
        private int started;
        private int completed;
        private int running;
        private readonly object syncRoot = new object();
        private ProxyServer proxy;
        private bool disposed;

        private Runner(Options options, LoadTestOptions verbOptions, Func<IPEndPoint, FastServer> serverFactory, Func<IPEndPoint, FastClient> clientFactory)
        {
            parallel = verbOptions.Parallel;
            count = verbOptions.Iterations * verbOptions.Parallel;
            this.options = options;
            this.serverFactory = serverFactory;
            this.clientFactory = clientFactory;
        }

        private void Run()
        {
            var server = serverFactory(new IPEndPoint(IPAddress.Loopback, 0));
            server.Start();

            IListener listener = this.listener;

            if (ParseUtils.ParseSize(options.Bandwidth) is int bandwidth)
                listener = new ThrottlingListener(this.listener, bandwidth);

            IConnector connector = new SimpleConnector(server.EndPoint, listener);

            if (options.Chaos)
            {
                var chaosConfiguration = new ChaosConfiguration
                {
                    Reject =
                    {
                        Percentage = 0.5
                    },
                    Abort =
                    {
                        Percentage = 1,
                        UpstreamBytes = new Range<long>(0, 1024 * 1024 * 10),
                        DownstreamBytes = new Range<long>(0, 1024 * 1024 * 10)
                    }
                };
                var chaosConnector = new ChaosConnector(chaosConfiguration, connector);

                //chaosConnector.Rejected += (s, e) => Console.WriteLine("REJECTED");
                //chaosConnector.Aborted += (s, e) => Console.WriteLine($"ABORTED reason {e.Reason}, upstream {e.UpstreamTransferred}, downstream {e.DownstreamTransferred}");

                connector = chaosConnector;
            }

            proxy = new ProxyServer(new IPEndPoint(IPAddress.Loopback, 0), connector);
            proxy.ExceptionOccured += (s, e) => Console.WriteLine($"EXCEPTION: {e.Exception.Message} ({e.Exception.GetType().FullName})");
            proxy.Start();

            started = parallel;

            using (var @event = new ManualResetEventSlim())
            {
                for (int i = 0; i < parallel; i++)
                {
                    ThreadPool.QueueUserWorkItem(p => Start(@event));
                }

                do
                {
                    PrintStatus();
                }
                while (!@event.Wait(TimeSpan.FromSeconds(1)));
            }

            PrintStatus();
        }

        private void Start(ManualResetEventSlim @event)
        {
            var client = clientFactory(proxy.EndPoint);

            client.Completed += (s, e) =>
            {
                lock (syncRoot)
                {
                    completed++;
                    running--;

                    if (started < count)
                    {
                        started++;
                        Start(@event);
                    }
                    else if (completed >= count)
                    {
                        @event.Set();
                    }
                }
            };

            client.Start();

            lock (syncRoot)
            {
                running++;
            }
        }

        private void PrintStatus()
        {
            double upstreamMb = (double)listener.AverageUpstream / (1024 * 1024);
            double downstreamMb = (double)listener.AverageDownstream / (1024 * 1024);

            Console.WriteLine($"Upstream {upstreamMb:0.00} mb/s, downstream {downstreamMb:0.00} mb/s, completed {completed}, running {running}");
        }

        public void Dispose()
        {
            if (!disposed)
            {
                DisposeUtils.DisposeSafely(ref proxy);

                disposed = true;
            }
        }
    }
}
