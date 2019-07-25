using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy.App
{
    internal class BulkServer : FastServer
    {
        private readonly int blockSize;
        private readonly int blockCount;

        public BulkServer(IPEndPoint endpoint, int blockSize, int blockCount, int backlog = DefaultBacklog)
            : base(endpoint, backlog)
        {
            this.blockSize = blockSize;
            this.blockCount = blockCount;
        }

        protected override IFastSocket CreateSocket(Socket client)
        {
            return new BulkSocket(client, blockSize, blockCount);
        }
    }
}
