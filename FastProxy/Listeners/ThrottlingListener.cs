using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FastProxy.Listeners
{
    public class ThrottlingListener : DelegatingListener
    {
        private readonly Channel upstream;
        private readonly Channel downstream;
        private Timer timer;
        private bool disposed;

        public ThrottlingListener(IListener inner, long bandwidth)
            : this(inner, bandwidth, bandwidth)
        {
        }

        public ThrottlingListener(IListener inner, long upstreamBandwidth, long downstreamBandwidth)
            : this(inner, upstreamBandwidth, downstreamBandwidth, 10)
        {
        }

        public ThrottlingListener(IListener inner, long upstreamBandwidth, long downstreamBandwidth, int slices)
            : base(inner)
        {
            upstream = new Channel(upstreamBandwidth, slices);
            downstream = new Channel(downstreamBandwidth, slices);
            timer = new Timer(TimerCallback, null, TimeSpan.FromSeconds(1.0 / slices), TimeSpan.FromSeconds(1.0 / slices));
        }

        private void TimerCallback(object state)
        {
            upstream.Schedule();
            downstream.Schedule();
        }

        public override OperationResult DataReceived(int bytesTransferred, Direction direction)
        {
            var result = base.DataReceived(bytesTransferred, direction);
            if (result.Outcome != OperationOutcome.Continue)
                return result;

            var channel = direction == Direction.Upstream ? upstream : downstream;

            return channel.GetResult(bytesTransferred);
        }

        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                DisposeUtils.DisposeSafely(ref timer);

                disposed = true;
            }

            base.Dispose(disposing);
        }

        private class Channel
        {
            private readonly int slices;
            private readonly long bandwidth;
            private long transferred;
            private readonly List<OperationContinuation> continuations = new List<OperationContinuation>();
            private long lastTime = Timestamp.Create().Ticks;
            private readonly object syncRoot = new object();

            public Channel(long bandwidth, int slices)
            {
                this.slices = slices;
                this.bandwidth = bandwidth / slices;
            }

            public OperationResult GetResult(int bytesTransferred)
            {
                // Add our bytes transferred to the total.

                long transferred = Interlocked.Add(ref this.transferred, bytesTransferred);

                // If we went over our budget, delay the continuation of this
                // transfer and schedule it to be executed later.

                if (transferred > bandwidth)
                {
                    lock (syncRoot)
                    {
                        var continuation = new OperationContinuation();
                        continuations.Add(continuation);
                        return continuation.Result;
                    }
                }

                return OperationResult.Continue;
            }

            public void Schedule()
            {
                List<OperationContinuation> continuations;

                // Calculate the budget. The timer is not exact, so we need to get the
                // time since the last time we calculated to recalculate the exact
                // budget we have.

                var time = Timestamp.Create();
                var lastTime = new Timestamp(Interlocked.Exchange(ref this.lastTime, time.Ticks));
                var elapsed = time - lastTime;

                long budget = (long)((elapsed.TotalSeconds * slices) * bandwidth);

                // Find out how much we've transferred and update the total transferred
                // value. We decrease the total transferred with the budget we had. However,
                // this would indefinitely increase the budget when the actual data
                // transferred is a lot lower than the bandwidth. We cap this at -budget.
                // We could just cap it at 0, but the problem is that the algorithm we're
                // using to throttle isn't exact, so a bit of leeway is helpful to spread
                // out the bandwidth usage.

                long oldTransferred;
                long newTransferred;

                do
                {
                    oldTransferred = transferred;
                    newTransferred = Math.Max(oldTransferred - budget, -budget);
                }
                while (Interlocked.CompareExchange(ref transferred, newTransferred, oldTransferred) != oldTransferred);

                // If we're still over budget, wait one more slice.

                if (newTransferred > budget)
                    return;

                // Get the list of continuations that were scheduled so we can execute them now.

                lock (syncRoot)
                {
                    if (this.continuations.Count == 0)
                        return;

                    continuations = new List<OperationContinuation>(this.continuations);
                    this.continuations.Clear();
                }

                foreach (var continuation in continuations)
                {
                    continuation.SetOutcome(OperationOutcome.Continue);
                }
            }
        }
    }
}
