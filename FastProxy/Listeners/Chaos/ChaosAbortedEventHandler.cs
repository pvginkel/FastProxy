using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy.Listeners.Chaos
{
    /// <summary>
    /// Provides the event arguments for the <see cref="ChaosAbortedEventHandler"/>
    /// event handler.
    /// </summary>
    public class ChaosAbortedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the reason a connection is aborted.
        /// </summary>
        public ChaosAbortReason Reason { get; }

        /// <summary>
        /// Gets the number of bytes transferred upstream when the connection
        /// was aborted.
        /// </summary>
        public long UpstreamTransferred { get; }

        /// <summary>
        /// Gets the number of bytes transferred downstream when the connection
        /// was aborted.
        /// </summary>
        public long DownstreamTransferred { get; }

        /// <summary>
        /// Initializes a new <see cref="ChaosAbortedEventArgs"/>.
        /// </summary>
        /// <param name="reason">The reason a connection is aborted.</param>
        /// <param name="upstreamTransferred">The number of bytes transferred upstream
        /// when the connection was aborted.</param>
        /// <param name="downstreamTransferred">The number of bytes transferred downstream
        /// when the connection was aborted.</param>
        public ChaosAbortedEventArgs(ChaosAbortReason reason, long upstreamTransferred, long downstreamTransferred)
        {
            Reason = reason;
            UpstreamTransferred = upstreamTransferred;
            DownstreamTransferred = downstreamTransferred;
        }
    }

    /// <summary>
    /// Represents the method called when a chaos connection is aborted.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">The event arguments.</param>
    public delegate void ChaosAbortedEventHandler(object sender, ChaosAbortedEventArgs e);
}
