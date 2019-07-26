using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy.Listeners.Chaos
{
    public class ChaosConfiguration
    {
        public ChaosRejectConfiguration Reject { get; } = new ChaosRejectConfiguration();
        public ChaosAbortConfiguration Abort { get; } = new ChaosAbortConfiguration();
    }

    public class ChaosRejectConfiguration
    {
        public double Percentage { get; set; }
    }

    public class ChaosAbortConfiguration
    {
        public double Percentage { get; set; }
        public Range<long> UpstreamBytes { get; set; }
        public Range<long> DownstreamBytes { get; set; }
        public Range<TimeSpan> Duration { get; set; }
    }
}
