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

            //RunBulk();

            for (int i = 0; i < 10; i++)
            {
                RunBulk();
            }
        }

        private static void RunBulk()
        {
            int blockSize = 4096;
            int blockCount = 100_000;

            var server = new BulkServer(new IPEndPoint(0, 0), blockSize, blockCount);
            server.Start();

            int clients = 10;
            int completed = 0;
            object syncRoot = new object();
            var stopwatch = Stopwatch.StartNew();

            using (var @event = new ManualResetEventSlim())
            {
                for (int i = 0; i < clients; i++)
                {
                    var client = new BulkClient(new IPEndPoint(IPAddress.Loopback, server.LocalEndPoint.Port), blockSize, blockCount);
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

        private static void RunEcho()
        {
            var buffer = new byte[4096];
            var blockCount = 100_000;

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
