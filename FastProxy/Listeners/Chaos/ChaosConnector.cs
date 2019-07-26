using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy.Listeners.Chaos
{
    public class ChaosConnector : IConnector
    {
        private readonly ChaosConfiguration configuration;
        private readonly IConnector inner;
        private readonly Random random = new Random();
        private readonly object syncRoot = new object();

        public event EventHandler Rejected;
        public event ChaosAbortedEventHandler Aborted;

        public ChaosConnector(ChaosConfiguration configuration, IConnector inner)
        {
            this.configuration = configuration;
            this.inner = inner;
        }

        public bool Connect(out IPEndPoint endpoint, out IListener listener)
        {
            if (!inner.Connect(out endpoint, out listener))
                return false;

            lock (syncRoot)
            {
                if (random.NextDouble() < configuration.Reject.Percentage)
                {
                    OnRejected();
                    return false;
                }

                listener = new ChaosListener(listener, configuration, random, this);
            }

            return true;
        }

        protected virtual void OnRejected() => Rejected?.Invoke(this, EventArgs.Empty);
        protected virtual void OnAborted(ChaosAbortedEventArgs e) => Aborted?.Invoke(this, e);

        internal void RaiseAborted(ChaosAbortReason reason, long upstreamTransferred, long downstreamTransferred)
        {
            OnAborted(new ChaosAbortedEventArgs(reason, upstreamTransferred, downstreamTransferred));
        }
    }
}
