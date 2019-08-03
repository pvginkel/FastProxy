using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FastProxy.App
{
    public class ProxyRunner : Runner
    {
        public static void Run(Options options, ServeOptions sharedOptions)
        {
            using (var runner = new ProxyRunner(options, sharedOptions))
            {
                runner.Run(sharedOptions.Port);
            }
        }

        private readonly ServeOptions sharedOptions;

        private ProxyRunner(Options options, ServeOptions sharedOptions)
            : base(options)
        {
            this.sharedOptions = sharedOptions;
        }

        protected override IPEndPoint GetServerEndpoint()
        {
            return new IPEndPoint(IPAddress.Loopback, sharedOptions.ServerPort);
        }

        protected override void Running(IPEndPoint endpoint)
        {
            using (var @event = new ManualResetEventSlim())
            {
                var thread = new Thread(() =>
                {
                    do
                    {
                        PrintStatus();
                    }
                    while (!@event.Wait(TimeSpan.FromSeconds(1)));

                    PrintStatus();
                });
                thread.Start();

                Console.WriteLine("Press enter to exit");
                Console.ReadLine();

                @event.Set();
                thread.Join();
            }
        }

        private void PrintStatus()
        {
            double upstreamMb = (double)Listener.AverageUpstream / (1024 * 1024);
            double downstreamMb = (double)Listener.AverageDownstream / (1024 * 1024);

            Console.WriteLine($"Upstream {upstreamMb:0.00} mb/s, downstream {downstreamMb:0.00} mb/s");
        }
    }
}
