using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy.Listeners
{
    /// <summary>
    /// Implements a sink listener.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The standard listeners all delegate to an inner listener, and providing one
    /// is required. If you do not have a custom listener to provide, you can use
    /// <see cref="Instance"/> to provide a sink listener instead.
    /// </para>
    /// </remarks>
    public class SinkListener : IListener
    {
        /// <summary>
        /// A shared instance of <see cref="SinkListener"/>.
        /// </summary>
        public static readonly SinkListener Instance = new SinkListener();

        /// <inheritdoc/>
        public void Closed()
        {
        }

        /// <inheritdoc/>
        public OperationResult DataReceived(int transferred, Direction direction)
        {
            return OperationResult.Continue;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
        }
    }
}
