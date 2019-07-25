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
        private readonly HashSet<BulkSocket> sockets = new HashSet<BulkSocket>();
        private readonly object syncRoot = new object();

        public BulkServer(IPEndPoint endpoint, int blockSize, int blockCount, int backlog = DefaultBacklog)
            : base(endpoint, backlog)
        {
            this.blockSize = blockSize;
            this.blockCount = blockCount;
        }

        protected override IFastSocket CreateSocket(Socket client)
        {
            var socket = new BulkSocket(client, blockSize, blockCount);

            lock (syncRoot)
            {
                sockets.Add(socket);
                socket.Completed += (s, e) =>
                {
                    lock (syncRoot)
                    {
                        sockets.Remove(socket);
                    }
                };
            }

            return socket;
        }
    }
}
