using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy
{
    /// <summary>
    /// Specifies the result of a <see cref="IConnector.Connect(out IPEndPoint, out IListener)"/>
    /// call.
    /// </summary>
    public enum ConnectResult
    {
        /// <summary>
        /// Indicates the connection should be accepted.
        /// </summary>
        Accept,

        /// <summary>
        /// Indicates the connection should be rejected.
        /// </summary>
        Reject
    }
}
