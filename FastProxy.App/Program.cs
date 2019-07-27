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

            var argList = new Queue<string>(args);

            int loops = 1;
            if (argList.Peek() == "loop")
            {
                argList.Dequeue();
                loops = int.Parse(argList.Dequeue());
            }

            string type = argList.Dequeue();

            int parallel = GetArgument(10);
            int blockSize = GetArgument(4096);
            int blockCount = GetArgument(10_000);

            int count = loops * parallel;

            switch (type)
            {
                case "echo":
                    RunEcho(parallel, count, blockSize, blockCount);
                    break;

                case "bulk":
                    RunBulk(parallel, count, blockSize, blockCount);
                    break;
            }

            int GetArgument(int defaultValue)
            {
                if (argList.Count > 0)
                    return int.Parse(argList.Dequeue());
                return defaultValue;
            }
        }

        private static void RunEcho(int parallel, int count, int blockSize, int blockCount)
        {
            var buffer = new byte[blockSize];

            new Random().NextBytes(buffer);

            Runner.Run(
                parallel,
                count,
                p => new EchoServer(p),
                p => new EchoClient(p, buffer, blockCount)
            );
        }

        private static void RunBulk(int parallel, int count, int blockSize, int blockCount)
        {
            Runner.Run(
                parallel,
                count,
                p => new BulkServer(p, blockSize, blockCount),
                p => new BulkClient(p, blockSize, blockCount)
            );
        }
    }
}
