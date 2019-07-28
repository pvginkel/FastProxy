using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FastProxy.Listeners.Chaos;

namespace FastProxy
{
    /// <summary>
    /// Provides an interface to integrate into <see cref="ProxyServer"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="IListener"/> interface allows you to integrate with
    /// <see cref="ProxyServer"/>.
    ///  </para>
    /// <para>
    /// The <see cref="DataReceived(int, Direction)"/> method is called
    /// whenever data is received from the server of the client. The return value
    /// of this method can be used to force a connect to be closed. This
    /// feature is e.g. used in <see cref="ChaosListener"/> to randomly close
    /// connections.
    /// </para>
    /// <para>
    /// Implementations of this interface are provided to <see cref="ProxyServer"/>
    /// through an <see cref="IConnector"/> implementation. This allows you to
    /// have different <see cref="IListener"/> instances per connection.
    /// </para>
    /// </remarks>
    public interface IListener : IDisposable
    {
        /// <summary>
        /// Called when the connection has been closed.
        /// </summary>
        void Closed();

        /// <summary>
        /// Called when bytes have been received from either the server or the
        /// client.
        /// </summary>
        /// <param name="bytesTransferred">The number of bytes received.</param>
        /// <param name="direction">The direction in which the data is going.</param>
        /// <returns>The action to take based on receiving this data.</returns>
        OperationResult DataReceived(int bytesTransferred, Direction direction);
    }
}
