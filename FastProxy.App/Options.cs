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

    public abstract class VerbOptions
    {
    }

    [Verb("serve", HelpText = "Serve just a proxy server")]
    public class ServeOptions : VerbOptions
    {
        [Option('p', "port", Default = 10783, HelpText = "The port where to serve the proxy server at")]
        public int Port { get; set; }

        [Option('s', "server-port", Default = 10784, HelpText = "The port where to forward connections to")]
        public int ServerPort { get; set; }
    }

    public abstract class LoadTestOptions : VerbOptions
    {
        [Option('i', "iterations", Default = 1, HelpText = "The number of iterations to run the test for")]
        public int Iterations { get; set; }

        [Option('p', "parallel", Default = 1, HelpText = "The number of clients to run in parallel")]
        public int Parallel { get; set; }

        [Option("external-proxy", HelpText = "Use an out of process proxu")]
        public bool ExternalProxy { get; set; }

        [Option("proxy-port", Default = 10783, HelpText = "The port of the proxy if the proxy is running out of process")]
        public int? ProxyPort { get; set; }

        [Option("server-port", Default = 10784, HelpText = "The port of the server if the proxy is running out of process")]
        public int? ServerPort { get; set; }
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
