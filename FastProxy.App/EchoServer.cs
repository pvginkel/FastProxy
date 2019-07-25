using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy.App
{
    internal class EchoServer : FastServer
    {
        public EchoServer(IPEndPoint endpoint, int backlog = DefaultBacklog)
            : base(endpoint, backlog)
        {
        }

        protected override IFastSocket CreateSocket(Socket client)
        {
            return new EchoSocket(client);
        }

        private class EchoSocket : IFastSocket
        {
            private Socket socket;
            private SocketAsyncEventArgs eventArgs;
            private readonly byte[] buffer = new byte[4096];
            private bool disposed;

            public event ExceptionEventHandler ExceptionOccured;

            public EchoSocket(Socket socket)
            {
                this.socket = socket;

                eventArgs = new SocketAsyncEventArgs();
                eventArgs.Completed += EventArgs_Completed;
            }

            public void Start()
            {
                StartReceive();
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

                var bytesTransferred = eventArgs.BytesTransferred;
                if (bytesTransferred == 0 || eventArgs.SocketError != SocketError.Success)
                {
                    Close();
                    return;
                }

                eventArgs.SetBuffer(buffer, 0, bytesTransferred);

                if (!socket.SendAsync(eventArgs))
                    EndSend();
            }

            private void EndSend()
            {
                var eventArgs = this.eventArgs;
                if (eventArgs.SocketError != SocketError.Success)
                {
                    Close();
                    return;
                }

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
