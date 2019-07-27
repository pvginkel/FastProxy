using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.CsProj;

namespace FastProxy.BenchmarkApp
{
    public class Core30JobAttribute : JobConfigBaseAttribute
    {
        public Core30JobAttribute(bool baseline = false)
            : base(Job.Default.With(CsProjCoreToolchain.NetCoreApp30).WithBaseline(baseline))
        {
        }
    }
}
