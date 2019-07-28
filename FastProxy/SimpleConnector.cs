using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy
{
    /// <summary>
    /// Provides a simple <see cref="IConnector"/> implementation that accepts
    /// every incoming connection and returns the same endpoint and listener for
    /// every connection.
    /// </summary>
    public class SimpleConnector : IConnector
    {
        private readonly IPEndPoint endpoint;
        private readonly IListener listener;

        /// <summary>
        /// Initializes a new <see cref="SimpleConnector"/>.
        /// </summary>
        /// <param name="endpoint">The upstream endpoint to proxy connections to.</param>
        /// <param name="listener">The listener to associated with incoming connections.</param>
        public SimpleConnector(IPEndPoint endpoint, IListener listener = null)
        {
            this.endpoint = endpoint;
            this.listener = listener;
        }

        /// <inheritdoc/>
        public ConnectResult Connect(out IPEndPoint endpoint, out IListener listener)
        {
            endpoint = this.endpoint;
            listener = this.listener;

            return ConnectResult.Accept;
        }
    }
}
