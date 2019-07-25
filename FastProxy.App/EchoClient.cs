using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
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

        protected override IFastSocket CreateSocket(Socket client)
        {
            return new EchoSocket(this, client);
        }

        protected virtual void OnCompleted()
        {
            Completed?.Invoke(this, EventArgs.Empty);
        }

        private class EchoSocket : IFastSocket
        {
            private readonly EchoClient client;
            private readonly byte[] buffer;
            private Socket socket;
            private SocketAsyncEventArgs eventArgs;
            private int remaining;
            private bool disposed;

            public event ExceptionEventHandler ExceptionOccured;

            public EchoSocket(EchoClient client, Socket socket)
            {
                this.client = client;
                this.socket = socket;

                remaining = client.blockCount;

                buffer = new byte[client.block.Length];

                eventArgs = new SocketAsyncEventArgs();
                eventArgs.Completed += EventArgs_Completed;
            }

            public void Start()
            {
                StartSend();
            }

            private void EventArgs_Completed(object sender, SocketAsyncEventArgs e)
            {
                switch (e.LastOperation)
                {
                    case SocketAsyncOperation.Send:
                        EndSend();
                        break;
                    case SocketAsyncOperation.Receive:
                        EndReceive();
                        break;
                }
            }

            private void StartSend()
            {
                var eventArgs = this.eventArgs;
                if (eventArgs == null)
                    return;

                eventArgs.SetBuffer(client.block, 0, client.block.Length);

                if (!socket.SendAsync(eventArgs))
                    EndSend();
            }

            private void EndSend()
            {
                StartReceive();
            }

            private void StartReceive()
            {
                var eventArgs = this.eventArgs;
                if (eventArgs == null)
                    return;

                eventArgs.SetBuffer(buffer, 0, buffer.Length);

                if (!socket.ReceiveAsync(eventArgs))
                    EndReceive();
            }

            private void EndReceive()
            {
                var eventArgs = this.eventArgs;
                if (eventArgs == null)
                    return;

                if (eventArgs.BytesTransferred == 0 || eventArgs.SocketError != SocketError.Success)
                {
                    Close();
                    return;
                }

                //Console.WriteLine("Received on client: " + Encoding.UTF8.GetString(buffer.ToArray()));

                //if (remaining % 10000 == 0)
                //    Console.WriteLine(remaining);

                if (--remaining > 0)
                    StartSend();
                else
                    client.OnCompleted();
            }

            private void Close()
            {
                try
                {
                    Dispose();
                }
                catch (Exception ex)
                {
                    OnExceptionOccured(new ExceptionEventArgs(ex));
                }
            }

            private void OnExceptionOccured(ExceptionEventArgs e)
            {
                ExceptionOccured?.Invoke(this, e);
            }

            public void Dispose()
            {
                if (!disposed)
                {
                    if (socket != null)
                    {
                        socket.Dispose();
                        socket = null;
                    }
                    if (eventArgs != null)
                    {
                        eventArgs.Dispose();
                        eventArgs = null;
                    }

                    disposed = true;
                }
            }
        }
    }
}
