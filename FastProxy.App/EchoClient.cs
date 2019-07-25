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

        private class EchoSocket : IFastSocket
        {
            private readonly EchoClient client;
            private Socket socket;
            private SocketAsyncEventArgs sendEventArgs;
            private SocketAsyncEventArgs receiveEventArgs;
            private int remaining;
            private bool disposed;

            public event ExceptionEventHandler ExceptionOccured;

            public EchoSocket(EchoClient client, Socket socket)
            {
                this.client = client;
                this.socket = socket;

                remaining = client.blockCount;

                sendEventArgs = new SocketAsyncEventArgs();
                sendEventArgs.SetBuffer(client.block, 0, client.block.Length);
                sendEventArgs.Completed += (s, e) => EndSend();

                var buffer = new byte[client.block.Length];

                receiveEventArgs = new SocketAsyncEventArgs();
                receiveEventArgs.SetBuffer(buffer, 0, buffer.Length);
                receiveEventArgs.Completed += (s, e) => EndReceive();
            }

            public void Start()
            {
                StartSend();
            }

            private void StartSend()
            {
                var eventArgs = sendEventArgs;
                if (eventArgs == null)
                    return;

                if (!socket.SendAsyncSuppressFlow(eventArgs))
                    EndSend();
            }

            private void EndSend()
            {
                StartReceive();
            }

            private void StartReceive()
            {
                var eventArgs = receiveEventArgs;
                if (eventArgs == null)
                    return;

                if (!socket.ReceiveAsyncSuppressFlow(eventArgs))
                    EndReceive();
            }

            private void EndReceive()
            {
                var eventArgs = receiveEventArgs;
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
                    socket?.Shutdown(SocketShutdown.Both);
                    DisposeUtils.DisposeSafely(ref socket);
                    DisposeUtils.DisposeSafely(ref sendEventArgs);
                    DisposeUtils.DisposeSafely(ref receiveEventArgs);

                    disposed = true;
                }
            }
        }
    }
}
