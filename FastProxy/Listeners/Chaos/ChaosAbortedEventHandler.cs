using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy.Listeners.Chaos
{
    public class ChaosAbortedEventArgs : EventArgs
    {
        public ChaosAbortReason Reason { get; }
        public long UpstreamTransferred { get; }
        public long DownstreamTransferred { get; }

        public ChaosAbortedEventArgs(ChaosAbortReason reason, long upstreamTransferred, long downstreamTransferred)
        {
            Reason = reason;
            UpstreamTransferred = upstreamTransferred;
            DownstreamTransferred = downstreamTransferred;
        }
    }

    public delegate void ChaosAbortedEventHandler(object sender, ChaosAbortedEventArgs e);
}
