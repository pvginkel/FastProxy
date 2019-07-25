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
            DebugListener.Setup();

            int offset = 0;

            int loops = 1;
            if (args[0] == "burn")
            {
                loops = int.Parse(args[1]);
                offset = 2;
            }

            string type = args[offset];

            int parallel = 10;
            if (args.Length > offset + 1)
                parallel = int.Parse(args[offset + 1]);
            int blockSize = 4096;
            if (args.Length > offset + 2)
                blockSize = int.Parse(args[offset + 2]);
            int blockCount = 10_000;
            if (args.Length > offset + 3)
                blockCount = int.Parse(args[offset + 3]);

            int count = loops * parallel;

            switch (type)
            {
                case "echo":
                    RunEcho(parallel, count, blockSize, blockCount);
                    break;

                case "bulk":
                    RunBulk(parallel, count, blockSize, blockCount);
                    break;
            }
        }

        private static void Run(int parallel, int count, Func<IPEndPoint, FastServer> serverFactory, Func<IPEndPoint, FastClient> clientFactory)
        {
            var server = serverFactory(new IPEndPoint(IPAddress.Loopback, 0));
            server.Start();

            var listener = new TransferredListener();

            var proxy = new ProxyServer(new IPEndPoint(IPAddress.Loopback, 0), new Connector(server.Endpoint, listener));
            proxy.ExceptionOccured += (s, e) => Console.WriteLine($"EXCEPTION: {e.Exception.Message} ({e.Exception.GetType().FullName})");
            proxy.Start();

            int started = parallel;
            int completed = 0;
            int running = 0;
            object syncRoot = new object();
            var stopwatch = Stopwatch.StartNew();
            var clients = new HashSet<FastClient>();

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
                while (!@event.Wait(TimeSpan.FromSeconds(1))) ;
            }

            PrintStatus();

            void Start(ManualResetEventSlim @event)
            {
                var client = clientFactory(proxy.Endpoint);

                client.Completed += (s, e) =>
                {
                    lock (syncRoot)
                    {
                        clients.Remove(client);

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
                    clients.Add(client);
                    running++;
                }
            }

            void PrintStatus()
            {
                double mbPerSecond = listener.GetAverage() / (1024 * 1024);

                Console.WriteLine($"MB per second: {mbPerSecond:0.00}, completed {completed}, running {running}");
            }
        }

        private static void RunEcho(int parallel, int count, int blockSize, int blockCount)
        {
            var buffer = new byte[blockSize];

            new Random().NextBytes(buffer);

            Run(
                parallel,
                count,
                p => new EchoServer(p),
                p => new EchoClient(p, buffer, blockCount)
            );
        }

        private static void RunBulk(int parallel, int count, int blockSize, int blockCount)
        {
            Run(
                parallel,
                count,
                p => new BulkServer(p, blockSize, blockCount),
                p => new BulkClient(p, blockSize, blockCount)
            );
        }

        private class Connector : IConnector
        {
            private readonly IPEndPoint endpoint;
            private readonly IListener listener;

            public Connector(IPEndPoint endpoint, IListener listener)
            {
                this.endpoint = endpoint;
                this.listener = listener;
            }

            public bool Connect(out IPEndPoint endpoint, out IListener listener)
            {
                endpoint = this.endpoint;
                listener = this.listener;
                return true;
            }
        }

        private class TransferredListener : IListener
        {
            private readonly Stopwatch stopwatch = Stopwatch.StartNew();
            private long last;
            private readonly object syncRoot = new object();
            private readonly Queue<double> averages = new Queue<double>();
            private TimeSpan lastElapsed;
            private long upstream;
            private long downstream;

            public long Upstream => upstream;
            public long Downstream => downstream;

            public void Connected()
            {
            }

            public void Closed()
            {
            }

            public OperationResult DataReceived(int bytesTransferred, Direction direction)
            {
                if (direction == Direction.Downstream)
                    Interlocked.Add(ref upstream, bytesTransferred);
                else
                    Interlocked.Add(ref downstream, bytesTransferred);

                return OperationResult.Continue;
            }

            public double GetAverage()
            {
                long transferred = Volatile.Read(ref upstream) + Volatile.Read(ref downstream);
                var elapsed = stopwatch.Elapsed;

                lock (syncRoot)
                {
                    long difference = transferred - last;
                    last = transferred;
                    double average = difference / (elapsed - lastElapsed).TotalSeconds;
                    lastElapsed = elapsed;

                    while (averages.Count > 5)
                    {
                        averages.Dequeue();
                    }

                    averages.Enqueue(average);

                    return averages.Average();
                }
            }
        }
    }
}
