using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy.Listeners.Chaos
{
    /// <summary>
    /// Provides configuration for the <see cref="ChaosConnector"/>.
    /// </summary>
    public class ChaosConfiguration
    {
        /// <summary>
        /// Gets the reject configuration.
        /// </summary>
        public ChaosRejectConfiguration Reject { get; } = new ChaosRejectConfiguration();

        /// <summary>
        /// Gets the abort configuration.
        /// </summary>
        public ChaosAbortConfiguration Abort { get; } = new ChaosAbortConfiguration();
    }

    /// <summary>
    /// Describes the rules for rejecting incoming connections.
    /// </summary>
    public class ChaosRejectConfiguration
    {
        /// <summary>
        /// Gets or sets the percentage of incoming connections that should
        /// be rejected.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The percentage is specified as a value between 0.0 and 1.0.
        /// </para>
        /// </remarks>
        public double Percentage { get; set; }
    }

    /// <summary>
    /// Describes the rules for aborting established connections.
    /// </summary>
    public class ChaosAbortConfiguration
    {
        /// <summary>
        /// Gets or sets the percentage of connections to which these rules
        /// apply.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The percentage is specified as a value between 0.0 and 1.0.
        /// </para>
        /// </remarks>
        public double Percentage { get; set; }

        /// <summary>
        /// Gets or sets the range of upstream bytes the connection will be
        /// allowed to transfer.
        /// </summary>
        public Range<long> UpstreamBytes { get; set; }

        /// <summary>
        /// Gets or sets the range of downstream bytes the connection will be
        /// allowed to transfer.
        /// </summary>
        public Range<long> DownstreamBytes { get; set; }

        /// <summary>
        /// Gets or sets the time span range after which the connection
        /// will be aborted.
        /// </summary>
        public Range<TimeSpan> Duration { get; set; }
    }
}
