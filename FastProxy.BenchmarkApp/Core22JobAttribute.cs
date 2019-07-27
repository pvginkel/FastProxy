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
    public class Core22JobAttribute : JobConfigBaseAttribute
    {
        public Core22JobAttribute(bool baseline = false)
            : base(Job.Default.With(CsProjCoreToolchain.NetCoreApp22).WithBaseline(baseline))
        {
        }
    }
}
