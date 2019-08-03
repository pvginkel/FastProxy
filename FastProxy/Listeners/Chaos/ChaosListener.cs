using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FastProxy.Listeners.Chaos
{
    internal class ChaosListener : DelegatingListener
    {
        private readonly ChaosConnector connector;
        private readonly Channel upstream;
        private readonly Channel downstream;

        public ChaosListener(IListener inner, ChaosConfiguration configuration, Random random, ChaosConnector connector)
            : base(inner)
        {
            this.connector = connector;
            bool abort = random.NextDouble() < configuration.Abort.Percentage;
            if (!abort)
                return;

            Timestamp? abortAfter = null;
            if (configuration.Abort.Duration != null)
            {
                var abortTimeSpan = GetRandomValue(random, configuration.Abort.Duration);
                abortAfter = Timestamp.Create() + abortTimeSpan;
            }
            long? abortUpstreamBytes = null;
            if (configuration.Abort.UpstreamBytes != null)
                abortUpstreamBytes = GetRandomValue(random, configuration.Abort.UpstreamBytes);
            long? abortDownstreamBytes = null;
            if (configuration.Abort.DownstreamBytes != null)
                abortDownstreamBytes = GetRandomValue(random, configuration.Abort.DownstreamBytes);

            upstream = new Channel(abortAfter, abortUpstreamBytes);
            downstream = new Channel(abortAfter, abortDownstreamBytes);
        }

        public override OperationResult DataReceived(int transferred, Direction direction)
        {
            var channel = direction == Direction.Upstream ? upstream : downstream;
            if (channel != null && channel.ShouldAbort(transferred, out var reason))
            {
                connector.RaiseAborted(reason, upstream.Transferred, downstream.Transferred);
                return OperationResult.CloseClient;
            }

            return base.DataReceived(transferred, direction);
        }

        private TimeSpan GetRandomValue(Random random, Range<TimeSpan> range)
        {
            return new TimeSpan(GetRandomValue(random, new Range<long>(range.Minimum.Ticks, range.Maximum.Ticks)));
        }

        private long GetRandomValue(Random random, Range<long> range)
        {
            int difference = (int)(range.Maximum - range.Minimum);
            int value = random.Next(0, difference);
            return range.Minimum + value;
        }

        private class Channel
        {
            private readonly Timestamp time;
            private readonly long bytes;
            private long transferred;
            private readonly bool abortFromTime;
            private readonly bool abortFromBytes;

            public long Transferred => Volatile.Read(ref transferred);

            public Channel(Timestamp? time, long? bytes)
            {
                this.time = time.GetValueOrDefault();
                this.bytes = bytes.GetValueOrDefault();

                abortFromTime = time.HasValue;
                abortFromBytes = bytes.HasValue;
            }

            public bool ShouldAbort(int bytesTransferred, out ChaosAbortReason reason)
            {
                reason = 0;

                if (abortFromTime && time <= Timestamp.Create())
                {
                    reason = ChaosAbortReason.TimeExpired;
                    return true;
                }

                if (abortFromBytes)
                {
                    long transferred = Interlocked.Add(ref this.transferred, bytesTransferred);
                    if (transferred > bytes)
                    {
                        reason = ChaosAbortReason.MaximumTransferred;
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
