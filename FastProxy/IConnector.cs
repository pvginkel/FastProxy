using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy
{
    /// <summary>
    /// Provides an interface for accepting incoming connections in a
    /// <see cref="ProxyServer"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="IConnector"/> allows integration with a <see cref="ProxyServer"/>.
    /// The <see cref="Connect(out IPEndPoint, out IListener)"/> method is called
    /// whenever a new client is trying to connect.
    /// </para>
    /// <para>
    /// On accepting a connection, the <c>Connect</c> method returns an endpoint and
    /// a listener. The endpoint is used to indicate the upstream endpoint to proxy
    /// the new client connection to. This can be used to e.g. implement a simple load
    /// balancer by returning a random endpoint from a list for every incoming connection.
    /// </para>
    /// <para>
    /// The listener can be either a shared single one for all connections, or one
    /// local to a specific connection. This allows you to track state or configuration
    /// for a specific connection.
    /// </para>
    /// </remarks>
    public interface IConnector
    {
        /// <summary>
        /// Called when a new client tries to connect.
        /// </summary>
        /// <param name="endpoint">The upstream endpoint to connect to.</param>
        /// <param name="listener">The <see cref="IListener"/> associated with this
        /// connection.</param>
        /// <returns>A <see cref="ConnectResult"/> indicating whether the connection
        /// should be accepted or rejected.</returns>
        ConnectResult Connect(out IPEndPoint endpoint, out IListener listener);
    }
}
