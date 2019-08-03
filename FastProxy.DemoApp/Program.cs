using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FastProxy.Listeners;
using FastProxy.Listeners.Chaos;
using FastProxy.TestSupport;

namespace FastProxy.DemoApp
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            // This application contains a number of demos used in the documentation. These
            // demos can be used as a starting point for embedding FastProxy into your own
            // project.

            ProxyEcho();
            LoadBalancedProxyEcho();
            BandwidthThrottlingProxyEcho();
            DelayTransferEchoServer();
            SimulateNetworkFailureEchoServer();
        }

        public static void ProxyEcho()
        {
            using (var echoServer = new EchoServer(new IPEndPoint(IPAddress.Loopback, 0)))
            {
                echoServer.Start();

                var connector = new SimpleConnector(echoServer.EndPoint);

                using (var proxyServer = new ProxyServer(new IPEndPoint(IPAddress.Loopback, 0), connector))
                {
                    proxyServer.Start();

                    var block = Encoding.UTF8.GetBytes("Hello world!");

                    using (var echoClient = new EchoPingClient(proxyServer.EndPoint, block))
                    {
                        echoClient.Start();

                        echoClient.Ping();
                    }
                }
            }
        }

        public static void LoadBalancedProxyEcho()
        {
            using (var echoServer1 = new EchoServer(new IPEndPoint(IPAddress.Loopback, 0)))
            using (var echoServer2 = new EchoServer(new IPEndPoint(IPAddress.Loopback, 0)))
            using (var echoServer3 = new EchoServer(new IPEndPoint(IPAddress.Loopback, 0)))
            using (var echoServer4 = new EchoServer(new IPEndPoint(IPAddress.Loopback, 0)))
            {
                echoServer1.Start();
                echoServer2.Start();
                echoServer3.Start();
                echoServer4.Start();

                var connector = new RoundRobinLoadBalancingConnector(
                    echoServer1.EndPoint,
                    echoServer2.EndPoint,
                    echoServer3.EndPoint,
                    echoServer4.EndPoint
                );

                using (var proxyServer = new ProxyServer(new IPEndPoint(IPAddress.Loopback, 0), connector))
                {
                    proxyServer.Start();

                    var block = Encoding.UTF8.GetBytes("Hello world!");

                    for (int i = 0; i < 32; i++)
                    {
                        using (var echoClient = new EchoPingClient(proxyServer.EndPoint, block))
                        {
                            echoClient.Start();

                            echoClient.Ping();
                        }
                    }
                }
            }
        }

        public class RoundRobinLoadBalancingConnector : IConnector
        {
            private readonly IPEndPoint[] endpoints;
            private int nextEndpoint;

            public RoundRobinLoadBalancingConnector(params IPEndPoint[] endpoints)
            {
                this.endpoints = endpoints;
            }

            public ConnectResult Connect(out IPEndPoint endpoint, out IListener listener)
            {
                int nextEndpoint = Interlocked.Increment(ref this.nextEndpoint);

                endpoint = endpoints[nextEndpoint % endpoints.Length];
                listener = null;

                return ConnectResult.Accept;
            }
        }

        public static void BandwidthThrottlingProxyEcho()
        {
            using (var echoServer = new EchoServer(new IPEndPoint(IPAddress.Loopback, 0)))
            {
                echoServer.Start();

                var listener = new ThrottlingListener(
                    SinkListener.Instance,
                    10 * 1024 /* 10 Kb/s */
                );
                var connector = new SimpleConnector(echoServer.EndPoint, listener);

                using (var proxyServer = new ProxyServer(new IPEndPoint(IPAddress.Loopback, 0), connector))
                {
                    proxyServer.Start();

                    var block = Encoding.UTF8.GetBytes(new string('?', 1024));

                    var stopwatch = Stopwatch.StartNew();

                    using (var echoClient = new EchoPingClient(proxyServer.EndPoint, block))
                    {
                        echoClient.Start();

                        for (int i = 0; i < 100; i++)
                        {
                            echoClient.Ping();
                        }
                    }

                    Console.WriteLine(stopwatch.Elapsed);
                }
            }
        }

        public static void DelayTransferEchoServer()
        {
            using (var echoServer = new EchoServer(new IPEndPoint(IPAddress.Loopback, 0)))
            {
                echoServer.Start();

                var listener = new FixedDelayListener(
                    SinkListener.Instance,
                    TimeSpan.FromSeconds(0.5)
                );
                var connector = new SimpleConnector(echoServer.EndPoint, listener);

                using (var proxyServer = new ProxyServer(new IPEndPoint(IPAddress.Loopback, 0), connector))
                {
                    proxyServer.Start();

                    var block = Encoding.UTF8.GetBytes("Hello world!");

                    using (var echoClient = new EchoPingClient(proxyServer.EndPoint, block))
                    {
                        echoClient.Start();

                        var stopwatch = Stopwatch.StartNew();

                        echoClient.Ping();

                        Console.WriteLine(stopwatch.Elapsed);
                    }
                }
            }
        }

        public class FixedDelayListener : DelegatingListener
        {
            private readonly TimeSpan delay;

            public FixedDelayListener(IListener inner, TimeSpan delay)
                : base(inner)
            {
                this.delay = delay;
            }

            public override OperationResult DataReceived(int bytesTransferred, Direction direction)
            {
                // Call the base listener and return that value if it's not continue.

                var result = base.DataReceived(bytesTransferred, direction);
                if (result.Outcome != OperationOutcome.Continue)
                    return result;

                // Create an operation continuation and schedule it to be ran after
                // some time.

                var continuation = new OperationContinuation();

                Task.Run(async () =>
                {
                    await Task.Delay(delay);

                    continuation.SetOutcome(OperationOutcome.Continue);
                });

                return continuation.Result;
            }
        }

        private static void SimulateNetworkFailureEchoServer()
        {
            using (var echoServer = new EchoServer(new IPEndPoint(IPAddress.Loopback, 0)))
            {
                echoServer.Start();

                var configuration = new ChaosConfiguration
                {
                    Reject =
                    {
                        Percentage = 0.5
                    }
                };
                var connector = new ChaosConnector(
                    configuration,
                    new SimpleConnector(echoServer.EndPoint)
                );

                using (var proxyServer = new ProxyServer(new IPEndPoint(IPAddress.Loopback, 0), connector))
                {
                    proxyServer.Start();

                    var block = Encoding.UTF8.GetBytes("Hello world!");

                    int errors = 0;

                    for (int i = 0; i < 100; i++)
                    {
                        using (var echoClient = new EchoPingClient(proxyServer.EndPoint, block))
                        {
                            echoClient.ExceptionOccured += (s, e) => Interlocked.Increment(ref errors);
                            echoClient.Start();
                            echoClient.Ping();
                        }
                    }

                    Console.WriteLine(errors);
                }
            }
        }
    }
}
