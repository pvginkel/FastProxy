using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

namespace FastProxy.BenchmarkApp
{
    public static class Program
    {
        public static void Main(string[] args)
        {
#if false

            const int blockSize = 8192;
            const int loops = 128;

            while (true)
            {
                Console.Write("RUN");

                var benchmark = new EchoBenchmark
                {
                    BlockSize = blockSize,
                    ContinueMode = ContinueMode.Direct
                };

                benchmark.Setup();

                var stopwatch = Stopwatch.StartNew();

                for (int i = 0; i < loops; i++)
                {
                    benchmark.Ping();
                }

                Console.WriteLine($" {(stopwatch.Elapsed.TotalMilliseconds / loops) * 1000:0.0} us");

                benchmark.Cleanup();
            }

#else

            var re = args.Length == 0 ? null : new Regex(args[0], RegexOptions.IgnoreCase);

            var benchmarks = typeof(Program).Assembly
                .GetTypes()
                .Where(p =>
                    !p.IsAbstract &&
                    p.GetMethods().Any(p1 => p1.GetCustomAttributes(typeof(BenchmarkAttribute), true).Length > 0) &&
                    (re == null || re.IsMatch(p.FullName)))
                .OrderBy(p => p.FullName)
                .ToArray();

            new BenchmarkSwitcher(benchmarks).RunAllJoined();

#endif
        }
    }
}
