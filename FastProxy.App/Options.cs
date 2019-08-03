using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;

namespace FastProxy.App
{
    public class Options
    {
        [Option('b', "bandwidth", HelpText = "Throttle bandwidth; supports K/M/G specifier")]
        public string Bandwidth { get; set; }

        [Option('c', "chaos", HelpText = "Enable the Chaos connector")]
        public bool Chaos { get; set; }
    }

    public abstract class LoadTestOptions
    {
        [Option('i', "iterations", Default = 1, HelpText = "The number of iterations to run the test for")]
        public int Iterations { get; set; }

        [Option('p', "parallel", Default = 1, HelpText = "The number of clients to run in parallel")]
        public int Parallel { get; set; }
    }

    [Verb("echo", HelpText = "Run the echo load tests")]
    public class EchoOptions : LoadTestOptions
    {
        [Option('s', "block-size", Default = "1K", HelpText = "The block size of data to transfer; supports K/M/G specifier")]
        public string BlockSize { get; set; }

        [Option('c', "block-count", Default = 1, HelpText = "The number of blocks to transfer per client")]
        public int BlockCount { get; set; }
    }

    [Verb("bulk", HelpText = "Run the bulk load tests")]
    public class BulkOptions : LoadTestOptions
    {
        [Option('s', "block-size", Default = "1K", HelpText = "The block size of data to transfer; supports K/M/G specifier")]
        public string BlockSize { get; set; }

        [Option('c', "block-count", Default = 1, HelpText = "The number of blocks to transfer per client")]
        public int BlockCount { get; set; }
    }
}
