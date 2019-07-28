using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy
{
    /// <summary>
    /// Specifies the outcome of a data transfer operation.
    /// </summary>
    public enum OperationOutcome
    {
        /// <summary>
        /// Indicates the data should be sent upstream or downstream as usual.
        /// </summary>
        Continue,

        /// <summary>
        /// Indicates that the client should be closed. Returning this value
        /// will not cause a graceful closure of the connection. Any outstanding
        /// data will be dropped and the client will be closed immediately.
        /// </summary>
        CloseClient,

        /// <summary>
        /// Indicates that the operation outcome is pending. This is used by the
        /// <see cref="OperationContinuation"/> to delay the operation outcome until
        /// a later time.
        /// </summary>
        Pending
    }
}
