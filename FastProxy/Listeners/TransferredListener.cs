using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FastProxy.Listeners
{
    /// <summary>
    /// Listener that tracks the number of bytes transferred.
    /// </summary>
    public class TransferredListener : DelegatingListener
    {
        private long upstream;
        private long downstream;

        /// <summary>
        /// Gets the number of upstream bytes transferred.
        /// </summary>
        public long Upstream => Volatile.Read(ref upstream);

        /// <summary>
        /// Gets the number of downstream bytes transferred.
        /// </summary>
        public long Downstream => Volatile.Read(ref downstream);

        /// <summary>
        /// Initializes a new <see cref="TransferredListener"/>.
        /// </summary>
        /// <param name="inner">The inner listener to delegate calls to.</param>
        public TransferredListener(IListener inner)
            : base(inner)
        {
        }

        /// <inheritdoc/>
        public override OperationResult DataReceived(int transferred, Direction direction)
        {
            if (direction == Direction.Upstream)
                Interlocked.Add(ref upstream, transferred);
            else
                Interlocked.Add(ref downstream, transferred);

            return base.DataReceived(transferred, direction);
        }
    }
}
