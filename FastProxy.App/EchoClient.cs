using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy.App
{
    internal class EchoClient : FastClient
    {
        private readonly int blockCount;
        private readonly byte[] block;

        public event EventHandler Completed;

        public EchoClient(IPEndPoint endpoint, byte[] block, int blockCount)
            : base(endpoint)
        {
            this.blockCount = blockCount;
            this.block = block;
        }

        protected override FastSocket CreateSocket(Socket client)
        {
            return new EchoSocket(this, client);
        }

        private class EchoSocket : FastSocket
        {
            private readonly EchoClient client;
            private int remaining;
            private bool sendPending;
            private bool echoPending;
            private readonly object syncRoot = new object();

            public EchoSocket(EchoClient client, Socket socket, int bufferSize = DefaultBufferSize)
                : base(socket, bufferSize)
            {
                this.client = client;
                this.remaining = client.blockCount;

                Send(new ArraySegment<byte>(client.block));
            }

            protected override void ProcessRead(ArraySegment<byte> buffer)
            {
                //Console.WriteLine("Received on client: " + Encoding.UTF8.GetString(buffer.ToArray()));

                if (remaining % 10000 == 0)
                    Console.WriteLine(remaining);

                if (--remaining > 0)
                    Send(new ArraySegment<byte>(client.block));
                else
                    client.OnCompleted();
            }

            protected override void SendComplete()
            {
                // Nothing to do.
            }
        }

        protected virtual void OnCompleted()
        {
            Completed?.Invoke(this, EventArgs.Empty);
        }
    }
}
