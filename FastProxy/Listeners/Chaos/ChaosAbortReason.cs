using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy.Listeners.Chaos
{
    /// <summary>
    /// Specifies the reason a chaos connection was aborted.
    /// </summary>
    public enum ChaosAbortReason
    {
        /// <summary>
        /// Indicates the timeout expires.
        /// </summary>
        TimeExpired,

        /// <summary>
        /// Indicates the maximum number of bytes was transferred.
        /// </summary>
        MaximumTransferred
    }
}
