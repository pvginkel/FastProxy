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
    public abstract class Runner : IDisposable
    {
        private readonly Options options;
        private ProxyServer proxy;
        private bool disposed;

        protected BandwidthListener Listener { get; } = new BandwidthListener(SinkListener.Instance);

        protected Runner(Options options)
        {
            this.options = options;
        }

        protected void Run(int port = 0)
        {
            var endpoint = GetServerEndpoint();

            IListener listener = Listener;

            if (ParseUtils.ParseSize(options.Bandwidth) is int bandwidth)
                listener = new ThrottlingListener(listener, bandwidth);

            IConnector connector = new SimpleConnector(endpoint, listener);

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

            proxy = new ProxyServer(new IPEndPoint(IPAddress.Loopback, port), connector);
            proxy.ExceptionOccured += (s, e) => Console.WriteLine($"EXCEPTION: {e.Exception.Message} ({e.Exception.GetType().FullName})");
            proxy.Start();

            Running(proxy.EndPoint);
        }

        protected abstract IPEndPoint GetServerEndpoint();

        protected abstract void Running(IPEndPoint endpoint);

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
