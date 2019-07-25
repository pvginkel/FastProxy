using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FastProxy.App
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            //RunEcho();

            //for (int i = 0; i < 10; i++)
            //{
            //    RunEcho();
            //}

            //RunBulk();

            for (int i = 0; i < 10; i++)
            {
                RunBulk();
            }
        }

        private static void RunBulk()
        {
            const int blockSize = 4096;
            const int blockCount = 10_000;
            //const int blockCount = 10;
            const int clients = 10;

            var server = new BulkServer(new IPEndPoint(IPAddress.Loopback, 0), blockSize, blockCount);
            server.Start();

            var proxy = new ProxyServer(new IPEndPoint(IPAddress.Loopback, 0), new Connector(server.LocalEndPoint));
            proxy.ExceptionOccured += (s, e) => Console.WriteLine($"EXCEPTION: {e.Exception.Message} ({e.Exception.GetType().FullName})");
            proxy.Start();

            int completed = 0;
            object syncRoot = new object();
            var stopwatch = Stopwatch.StartNew();

            using (var @event = new ManualResetEventSlim())
            {
                for (int i = 0; i < clients; i++)
                {
                    var client = new BulkClient(proxy.Endpoint, blockSize, blockCount);
                    client.Completed += (s, e) =>
                    {
                        lock (syncRoot)
                        {
                            if (++completed >= clients)
                                @event.Set();
                        }
                    };
                    client.Start();
                }

                @event.Wait();
            }

            Console.WriteLine(stopwatch.Elapsed);

            long total = (long)blockSize * (long)blockCount * (long)clients;
            double bytesPerSecond = total / stopwatch.Elapsed.TotalSeconds;
            double mbPerSecond = bytesPerSecond / (1024 * 1024);
            Console.WriteLine($"MB per second: {mbPerSecond:0.00}");
        }

        private class Connector : IConnector
        {
            private readonly IPEndPoint endpoint;

            public Connector(IPEndPoint endpoint)
            {
                this.endpoint = endpoint;
            }

            public bool Connect(out IPEndPoint endpoint, out IListener listener)
            {
                endpoint = this.endpoint;
                listener = null;
                //listener = new Listener();
                return true;
            }
        }

        private class Listener : IListener
        {
            public void Connected()
            {
                Console.WriteLine("CONNECTED");
            }

            public void Closed()
            {
                Console.WriteLine("CLOSED");
            }

            public OperationResult DataReceived(ref ArraySegment<byte> buffer, Direction direction)
            {
                //Console.WriteLine($"RECEIVED: direction {direction}, buffer {buffer.Count}");
                return OperationResult.Continue;
            }
        }

        private static void RunEcho()
        {
            var buffer = new byte[4096];
            var blockCount = 20_000;

            new Random().NextBytes(buffer);

            var server = new EchoServer(new IPEndPoint(0, 0));
            server.Start();
            var client = new EchoClient(new IPEndPoint(IPAddress.Loopback, server.LocalEndPoint.Port), buffer, blockCount);

            var stopwatch = Stopwatch.StartNew();

            using (var @event = new ManualResetEventSlim())
            {
                client.Completed += (s, e) => @event.Set();
                client.Start();
                @event.Wait();
            }

            Console.WriteLine(stopwatch.Elapsed);

            int total = buffer.Length * blockCount;
            double bytesPerSecond = total / stopwatch.Elapsed.TotalSeconds;
            double mbPerSecond = bytesPerSecond / (1024 * 1024);
            Console.WriteLine($"MB per second: {mbPerSecond:0.00}");
        }
    }
}
