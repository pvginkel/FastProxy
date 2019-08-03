using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FastProxy.TestSupport;

namespace FastProxy.App
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            DebugListener.Setup();

            if (!ParseUtils.ParseArguments<Options, VerbOptions>(args, out var sharedOptions, out var verbOptions))
                return;

            switch (verbOptions)
            {
                case ServeOptions serveOptions:
                    RunProxy(sharedOptions, serveOptions);
                    break;
                case EchoOptions echoOptions:
                    RunEcho(sharedOptions, echoOptions);
                    break;
                case BulkOptions bulkOptions:
                    RunBulk(sharedOptions, bulkOptions);
                    break;
            }
        }

        private static void RunProxy(Options options, ServeOptions verbOptions)
        {
            ProxyRunner.Run(options, verbOptions);
        }

        private static void RunEcho(Options options, EchoOptions verbOptions)
        {
            var buffer = new byte[ParseUtils.ParseSize(verbOptions.BlockSize).Value];

            new Random().NextBytes(buffer);

            LoadTestRunner.Run(
                options,
                verbOptions,
                p => new EchoServer(p),
                p => new EchoClient(p, buffer, verbOptions.BlockCount)
            );
        }

        private static void RunBulk(Options options, BulkOptions verbOptions)
        {
            LoadTestRunner.Run(
                options,
                verbOptions,
                p => new BulkServer(p, ParseUtils.ParseSize(verbOptions.BlockSize).Value, verbOptions.BlockCount),
                p => new BulkClient(p, ParseUtils.ParseSize(verbOptions.BlockSize).Value, verbOptions.BlockCount)
            );
        }
    }
}
