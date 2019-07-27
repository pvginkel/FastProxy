using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FastProxy.TestSupport
{
    public class EchoPingClient : FastClient
    {
        private readonly byte[] block;
        private EchoSocket socket;

        public EchoPingClient(IPEndPoint endpoint, byte[] block)
            : base(endpoint)
        {
            this.block = block;
        }

        protected override IFastSocket CreateSocket(Socket client)
        {
            socket = new EchoSocket(this, client);
            return socket;
        }

        public void Ping()
        {
            socket.Ping();
        }

        private class EchoSocket : IFastSocket
        {
            private readonly EchoPingClient client;
            private Socket socket;
            private SocketAsyncEventArgs sendEventArgs;
            private SocketAsyncEventArgs receiveEventArgs;
            private ManualResetEventSlim @event = new ManualResetEventSlim();
            private long pending;
            private bool disposed;

            public event ExceptionEventHandler ExceptionOccured;

            public EchoSocket(EchoPingClient client, Socket socket)
            {
                this.client = client;
                this.socket = socket;

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
                // Ignore.
            }

            public void Ping()
            {
                @event.Reset();

                Interlocked.Add(ref pending, client.block.Length);

                StartSend();

                @event.Wait();
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

                var transferred = eventArgs.BytesTransferred;
                if (transferred == 0 || eventArgs.SocketError != SocketError.Success)
                {
                    Close();
                    return;
                }

                if (Interlocked.Add(ref pending, -transferred) <= 0)
                    @event.Set();
                else
                    StartReceive();
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
                    DisposeUtils.DisposeSafely(ref @event);

                    disposed = true;
                }
            }
        }
    }
}
