using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy.App
{
    internal class BulkSocket : FastSocket
    {
        private readonly byte[] buffer;
        private int blockCount;
        private int receive;
        private readonly object syncRoot = new object();

        public event EventHandler Completed;

        public BulkSocket(Socket socket, int blockSize, int blockCount)
            : base(socket, blockSize)
        {
            this.buffer = new byte[blockSize];
            this.blockCount = blockCount;
            this.receive = blockSize * blockCount;

            StartSend();
        }

        protected override void ProcessRead(ArraySegment<byte> buffer)
        {
            lock (syncRoot)
            {
                receive -= buffer.Count;

                if (receive <= 0 && blockCount <= 0)
                {
                    Close();
                    OnCompleted();
                }
            }
        }

        protected override void SendComplete()
        {
            StartSend();
        }

        private void StartSend()
        {
            bool send = false;

            lock (syncRoot)
            {
                if (blockCount <= 0)
                {
                    if (receive <= 0)
                    {
                        Close();
                        OnCompleted();
                    }
                }
                else
                {
                    blockCount--;
                    send = true;
                }
            }

            if (send)
                Send(new ArraySegment<byte>(buffer));
        }

        protected virtual void OnCompleted()
        {
            Completed?.Invoke(this, EventArgs.Empty);
        }
    }
}
