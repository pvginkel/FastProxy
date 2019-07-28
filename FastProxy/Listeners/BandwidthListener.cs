using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FastProxy.Listeners
{
    /// <summary>
    /// Listener that tracks bandwidth usage.
    /// </summary>
    public class BandwidthListener : TransferredListener
    {
        private Timer timer;
        private bool disposed;
        private Timestamp lastTime;
        private long lastUpstream;
        private long averageUpstream;
        private long lastDownstream;
        private long averageDownstream;

        /// <summary>
        /// Gets the upstream bandwidth usage over the last second.
        /// </summary>
        public long AverageUpstream => Volatile.Read(ref averageUpstream);

        /// <summary>
        /// Gets the downstream bandwidth usage over the last second.
        /// </summary>
        public long AverageDownstream => Volatile.Read(ref averageDownstream);

        /// <summary>
        /// Initializes a new <see cref="BandwidthListener"/>.
        /// </summary>
        /// <param name="inner">The inner listener to delegate calls to.</param>
        public BandwidthListener(IListener inner)
            : base(inner)
        {
            timer = new Timer(TimerCallback, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            lastTime = Timestamp.Create();
        }

        private void TimerCallback(object state)
        {
            var time = Timestamp.Create();
            var elapsed = time - lastTime;

            Update(elapsed, ref lastUpstream, ref averageUpstream, Upstream);
            Update(elapsed, ref lastDownstream, ref averageDownstream, Downstream);

            lastTime = time;
        }

        private static void Update(TimeSpan elapsed, ref long last, ref long average, long current)
        {
            long difference = current - last;

            Volatile.Write(ref average, (long)(difference / elapsed.TotalSeconds));

            last = current;
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
    }
}
