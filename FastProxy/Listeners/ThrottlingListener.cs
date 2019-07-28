using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FastProxy.Listeners
{
    /// <summary>
    /// Listener that throttles bandwidth usage.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This listener throttles the bandwidth of connections. This is done by
    /// delaying forwarding data for a little while if the budget for an interval
    /// is exceeded.
    /// </para>
    /// <para>
    /// The <c>slices</c> parameter to the constructor specifies the number of
    /// time interval used to throttle connections. The default value for this
    /// parameter is 10. This means that the transfer budget is decided for
    /// every 100ms. If in that interval a connection exceeds its allotted budget,
    /// this data transfer and all following ones are delayed until a timer
    /// configured for the same interval fires.
    /// </para>
    /// <para>
    /// This listener implements a best effort algorithm to throttle bandwidth
    /// usage. It's not exact, and not meant to be. The primary use case is for
    /// use in (load) testing applications to e.g. simulate a bad internet connection.
    /// </para>
    /// </remarks>
    public class ThrottlingListener : DelegatingListener
    {
        private readonly Channel upstream;
        private readonly Channel downstream;
        private Timer timer;
        private bool disposed;

        /// <summary>
        /// Initializes a new <see cref="ThrottlingListener"/>.
        /// </summary>
        /// <param name="inner">The inner listener to delegate calls to.</param>
        /// <param name="bandwidth">The number of bytes per second allowed.</param>
        public ThrottlingListener(IListener inner, long bandwidth)
            : this(inner, bandwidth, bandwidth)
        {
        }

        /// <summary>
        /// Initializes a new <see cref="ThrottlingListener"/>.
        /// </summary>
        /// <param name="inner">The inner listener to delegate calls to.</param>
        /// <param name="upstreamBandwidth">The number of upstream bytes per second allowed.</param>
        /// <param name="downstreamBandwidth">The number of downstream bytes per second allowed.</param>
        public ThrottlingListener(IListener inner, long upstreamBandwidth, long downstreamBandwidth)
            : this(inner, upstreamBandwidth, downstreamBandwidth, 10)
        {
        }

        /// <summary>
        /// Initializes a new <see cref="ThrottlingListener"/>.
        /// </summary>
        /// <param name="inner">The inner listener to delegate calls to.</param>
        /// <param name="upstreamBandwidth">The number of upstream bytes per second allowed.</param>
        /// <param name="downstreamBandwidth">The number of downstream bytes per second allowed.</param>
        /// <param name="slices">The number of slices of a second in which budgets are calculated.</param>
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

        /// <inheritdoc/>
        public override OperationResult DataReceived(int bytesTransferred, Direction direction)
        {
            var result = base.DataReceived(bytesTransferred, direction);
            if (result.Outcome != OperationOutcome.Continue)
                return result;

            var channel = direction == Direction.Upstream ? upstream : downstream;

            return channel.GetResult(bytesTransferred);
        }

        /// <inheritdoc/>
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
