using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy.App
{
    internal class BulkClient : FastClient
    {
        private readonly int blockSize;
        private readonly int blockCount;

        public event EventHandler Completed;

        public BulkClient(IPEndPoint endpoint, int blockSize, int blockCount)
            : base(endpoint)
        {
            this.blockSize = blockSize;
            this.blockCount = blockCount;
        }

        protected override FastSocket CreateSocket(Socket client)
        {
            var socket = new BulkSocket(client, blockSize, blockCount);
            socket.Completed += (s, e) => OnCompleted();
            return socket;
        }

        protected virtual void OnCompleted()
        {
            Completed?.Invoke(this, EventArgs.Empty);
        }
    }
}
