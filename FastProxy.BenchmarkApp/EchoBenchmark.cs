using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using FastProxy.TestSupport;

namespace FastProxy.BenchmarkApp
{
    [MemoryDiagnoser]
    [ClrJob, Core22Job]
    public class EchoBenchmark
    {
        private ProxyServer proxy;
        private EchoServer echoServer;
        private EchoPingClient echoClient;
        private BlockingCollection<OperationContinuation> queue;
        private Thread thread;

        [Params(1, 16, 512, 4096, 8192)]
        public int BlockSize { get; set; }

        [Params(ContinueMode.Direct, ContinueMode.Continuation, ContinueMode.Scheduled)]
        public ContinueMode ContinueMode { get; set; }

        [GlobalSetup]
        public void Setup()
        {
            echoServer = new EchoServer(new IPEndPoint(IPAddress.Loopback, 0));
            echoServer.Start();

            if (ContinueMode == ContinueMode.Scheduled)
            {
                queue = new BlockingCollection<OperationContinuation>();

                thread = new Thread(() =>
                {
                    foreach (var continuation in queue.GetConsumingEnumerable())
                    {
                        continuation.SetOutcome(OperationOutcome.Continue);
                    }
                });
                thread.Start();
            }

            IListener listener = null;

            switch (ContinueMode)
            {
                case ContinueMode.Continuation:
                    listener = new CompletedContinuationListener();
                    break;
                case ContinueMode.Scheduled:
                    listener = new ScheduledContinuationListener(queue);
                    break;
            }

            proxy = new ProxyServer(new IPEndPoint(IPAddress.Loopback, 0), new SimpleConnector(echoServer.EndPoint, listener));
            proxy.Start();

            var buffer = new byte[BlockSize];
            new Random().NextBytes(buffer);

            echoClient = new EchoPingClient(proxy.EndPoint, buffer);
            echoClient.Start();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            using (queue)
            {
                queue?.CompleteAdding();
                thread?.Join();
            }

            echoClient.Dispose();
            proxy.Dispose();
            echoServer.Dispose();
        }

        [Benchmark]
        public void Ping()
        {
            echoClient.Ping();
        }

        private class CompletedContinuationListener : IListener
        {
            public void Closed()
            {
            }

            public OperationResult DataReceived(int bytesTransferred, Direction direction)
            {
                var continuation = new OperationContinuation();
                continuation.SetOutcome(OperationOutcome.Continue);
                return continuation.Result;
            }

            public void Dispose()
            {
            }
        }

        private class ScheduledContinuationListener : IListener
        {
            private readonly BlockingCollection<OperationContinuation> queue;

            public ScheduledContinuationListener(BlockingCollection<OperationContinuation> queue)
            {
                this.queue = queue;
            }

            public void Connected()
            {
            }

            public void Closed()
            {
            }

            public OperationResult DataReceived(int bytesTransferred, Direction direction)
            {
                var continuation = new OperationContinuation();
                queue.Add(continuation);
                return continuation.Result;
            }

            public void Dispose()
            {
            }
        }
    }
}
