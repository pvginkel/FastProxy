using System;
using System.Collections.Generic;
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

            while (true)
            {
                Console.WriteLine("RUN");

                var benchmark = new EchoBenchmark();
                benchmark.BlockSize = 4096;
                benchmark.ContinueMode = ContinueMode.Continuation;
                benchmark.Setup();
                for (int i = 0; i < 1000; i++)
                {
                    benchmark.Ping();
                }
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
