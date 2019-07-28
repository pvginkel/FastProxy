using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy.Listeners
{
    /// <summary>
    /// Implements a delegating listener for use as a base class for custom
    /// listeners.
    /// </summary>
    public class DelegatingListener : IListener
    {
        private readonly IListener inner;

        /// <summary>
        /// Initializes a new <see cref="DelegatingListener"/>.
        /// </summary>
        /// <param name="inner">The inner listener to delegate to.</param>
        public DelegatingListener(IListener inner)
        {
            this.inner = inner;
        }

        /// <inheritdoc/>
        public virtual void Closed()
        {
            inner.Closed();
        }

        /// <inheritdoc/>
        public virtual OperationResult DataReceived(int bytesTransferred, Direction direction)
        {
            return inner.DataReceived(bytesTransferred, direction);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Called when the object is being disposed.
        /// </summary>
        /// <param name="disposing">Whether this method is called from <see cref="Dispose()"/>.</param>
        protected virtual void Dispose(bool disposing)
        {
            inner.Dispose();
        }
    }
}
