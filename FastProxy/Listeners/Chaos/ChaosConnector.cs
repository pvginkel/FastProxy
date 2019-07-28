using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy.Listeners.Chaos
{
    /// <summary>
    /// Implements a <see cref="IConnector"/> that randomly closes connections.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="ChaosConnector"/> configures a <see cref="ProxyServer"/>
    /// to randomly close connections following rules described in a
    /// <see cref="ChaosConfiguration"/>. This connector is useful for load testers
    /// for applications that need to be able to gracefully support connection drops.
    /// </para>
    /// </remarks>
    public class ChaosConnector : IConnector
    {
        private readonly ChaosConfiguration configuration;
        private readonly IConnector inner;
        private readonly Random random = new Random();
        private readonly object syncRoot = new object();

        /// <summary>
        /// Raised when an incoming connect is rejected.
        /// </summary>
        public event EventHandler Rejected;

        /// <summary>
        /// Raised when a connection is aborted.
        /// </summary>
        public event ChaosAbortedEventHandler Aborted;

        /// <summary>
        /// Initializes a new <see cref="ChaosConnector"/>.
        /// </summary>
        /// <param name="configuration">The configuration associated with the connector.</param>
        /// <param name="inner">The inner connector used to get the upstream endpoint
        /// and listener.</param>
        public ChaosConnector(ChaosConfiguration configuration, IConnector inner)
        {
            this.configuration = configuration;
            this.inner = inner;
        }

        /// <inheritdoc/>
        public ConnectResult Connect(out IPEndPoint endpoint, out IListener listener)
        {
            var result = inner.Connect(out endpoint, out listener);
            if (result != ConnectResult.Accept)
                return result;

            lock (syncRoot)
            {
                if (random.NextDouble() < configuration.Reject.Percentage)
                {
                    OnRejected();
                    return ConnectResult.Reject;
                }

                listener = new ChaosListener(listener, configuration, random, this);
            }

            return ConnectResult.Accept;
        }

        /// <summary>
        /// Raises the <see cref="Rejected"/> event.
        /// </summary>
        protected virtual void OnRejected() => Rejected?.Invoke(this, EventArgs.Empty);

        /// <summary>
        /// Raises the <see cref="Aborted"/> event.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnAborted(ChaosAbortedEventArgs e) => Aborted?.Invoke(this, e);

        internal void RaiseAborted(ChaosAbortReason reason, long upstreamTransferred, long downstreamTransferred)
        {
            OnAborted(new ChaosAbortedEventArgs(reason, upstreamTransferred, downstreamTransferred));
        }
    }
}
