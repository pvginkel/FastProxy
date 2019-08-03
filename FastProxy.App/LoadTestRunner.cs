using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FastProxy.TestSupport;

namespace FastProxy.App
{
    public class LoadTestRunner : Runner
    {
        public static void Run(Options options, LoadTestOptions verbOptions, Func<IPEndPoint, FastServer> serverFactory, Func<IPEndPoint, FastClient> clientFactory)
        {
            using (var runner = new LoadTestRunner(options, verbOptions, serverFactory, clientFactory))
            {
                if (verbOptions.ExternalProxy)
                    runner.RunExternal();
                else
                    runner.Run();
            }
        }

        private readonly Func<IPEndPoint, FastServer> serverFactory;
        private readonly Func<IPEndPoint, FastClient> clientFactory;
        private readonly LoadTestOptions verbOptions;
        private int started;
        private int completed;
        private int running;
        private readonly object syncRoot = new object();
        private FastServer server;

        private LoadTestRunner(Options options, LoadTestOptions verbOptions, Func<IPEndPoint, FastServer> serverFactory, Func<IPEndPoint, FastClient> clientFactory)
            : base(options)
        {
            this.serverFactory = serverFactory;
            this.clientFactory = clientFactory;
            this.verbOptions = verbOptions;
        }

        protected void RunExternal()
        {
            GetServerEndpoint();

            Running(new IPEndPoint(IPAddress.Loopback, verbOptions.ProxyPort.Value));
        }

        protected override IPEndPoint GetServerEndpoint()
        {
            int serverPort = 0;
            if (verbOptions.ExternalProxy)
                serverPort = verbOptions.ServerPort.Value;

            server = serverFactory(new IPEndPoint(IPAddress.Loopback, serverPort));
            server.Start();

            return server.EndPoint;
        }

        protected override void Running(IPEndPoint endpoint)
        {
            started = verbOptions.Parallel;

            using (var @event = new ManualResetEventSlim())
            {
                for (int i = 0; i < verbOptions.Parallel; i++)
                {
                    ThreadPool.QueueUserWorkItem(p => Start(endpoint, @event));
                }

                do
                {
                    PrintStatus();
                }
                while (!@event.Wait(TimeSpan.FromSeconds(1)));
            }

            PrintStatus();
        }

        private void PrintStatus()
        {
            if (verbOptions.ExternalProxy)
            {
                Console.WriteLine($"Completed {completed}, running {running}");
            }
            else
            {
                double upstreamMb = (double)Listener.AverageUpstream / (1024 * 1024);
                double downstreamMb = (double)Listener.AverageDownstream / (1024 * 1024);

                Console.WriteLine($"Upstream {upstreamMb:0.00} mb/s, downstream {downstreamMb:0.00} mb/s, completed {completed}, running {running}");
            }
        }

        private void Start(IPEndPoint endpoint, ManualResetEventSlim @event)
        {
            int count = verbOptions.Parallel * verbOptions.Iterations;

            var client = clientFactory(endpoint);

            client.Completed += (s, e) =>
            {
                lock (syncRoot)
                {
                    completed++;
                    running--;

                    if (started < count)
                    {
                        started++;
                        Start(endpoint, @event);
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
    }
}
