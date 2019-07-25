using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FastProxy.App
{
    internal class TransferredListener : IListener
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