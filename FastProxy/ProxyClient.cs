using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy
{
    internal class ProxyClient : IDisposable
    {
        private Socket source;
        private Socket target;
        private Channel upstream;
        private Channel downstream;
        private readonly IPEndPoint endpoint;
        private readonly IListener listener;
        private readonly int bufferSize;
        private bool disposed;

        public event ExceptionEventHandler ExceptionOccured;
        public event EventHandler Closed;

        public ProxyClient(Socket client, IPEndPoint endpoint, IListener listener, int bufferSize)
        {
            this.source = client;
            this.endpoint = endpoint;
            this.listener = listener;
            this.bufferSize = bufferSize;
        }

        public void Start()
        {
            var eventArgs = new SocketAsyncEventArgs();
            eventArgs.Completed += ConnectEventArgs_Completed;

            try
            {
                target = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                eventArgs.RemoteEndPoint = endpoint;
                if (!target.ConnectAsync(eventArgs))
                    EndConnect(eventArgs);
            }
            catch (Exception ex)
            {
                CloseSafely(ex);
            }
        }

        private void ConnectEventArgs_Completed(object sender, SocketAsyncEventArgs e)
        {
            EndConnect(e);
        }

        private void EndConnect(SocketAsyncEventArgs eventArgs)
        {
            Debug.Assert(eventArgs.ConnectSocket == target);

            if (target.Connected)
            {
                try
                {
                    source.LingerState = new LingerOption(false, 0);
                    target.LingerState = new LingerOption(false, 0);

                    listener?.Connected();

                    // Reuse the connect event args.

                    eventArgs.Completed -= ConnectEventArgs_Completed;

                    upstream = new Channel(Direction.Upstream, target, source, this, eventArgs);
                    downstream = new Channel(Direction.Downstream, source, target, this, null);
                }
                catch (Exception ex)
                {
                    CloseSafely(ex);
                }
            }
            else
            {
                CloseSafely();
            }
        }

        private void CloseSafely(Exception exception = null)
        {
            try
            {
                Dispose();
            }
            catch (Exception ex)
            {
                if (exception == null)
                    exception = ex;
            }

            try
            {
                listener?.Closed();
            }
            catch (Exception ex)
            {
                if (exception == null)
                    exception = ex;
            }

            OnClosed();

            if (exception != null)
                OnExceptionOccured(new ExceptionEventArgs(exception));
        }

        protected virtual void OnExceptionOccured(ExceptionEventArgs e)
        {
            ExceptionOccured?.Invoke(this, e);
        }

        protected virtual void OnClosed()
        {
            Closed?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (!disposed)
            {
                if (source != null)
                {
                    source.Dispose();
                    source = null;
                }
                if (target != null)
                {
                    target.Dispose();
                    target = null;
                }
                if (upstream != null)
                {
                    upstream.Dispose();
                    upstream = null;
                }
                if (downstream != null)
                {
                    downstream.Dispose();
                    downstream = null;
                }

                disposed = true;
            }
        }

        private class Channel : IDisposable
        {
            private readonly Direction direction;
            private readonly Socket source;
            private readonly Socket target;
            private readonly ProxyClient client;
            private SocketAsyncEventArgs eventArgs;
            private bool disposed;
            private readonly byte[] buffer;

            public Channel(Direction direction, Socket source, Socket target, ProxyClient client, SocketAsyncEventArgs eventArgs)
            {
                this.direction = direction;
                this.source = source;
                this.target = target;
                this.client = client;

                this.buffer = new byte[client.bufferSize];

                this.eventArgs = eventArgs ?? new SocketAsyncEventArgs();
                this.eventArgs.SetBuffer(buffer, 0, buffer.Length);
                this.eventArgs.Completed += EventArgs_Completed;

                StartReceive();
            }

            private void StartReceive()
            {
                var eventArgs = this.eventArgs;
                if (eventArgs == null)
                    return;

                try
                {
                    if (!source.ReceiveAsync(eventArgs))
                        EndReceive();
                }
                catch (Exception ex)
                {
                    client.CloseSafely(ex);
                }
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
                    default:
                        throw new InvalidOperationException();
                }
            }

            private void EndSend()
            {
                var eventArgs = this.eventArgs;
                if (eventArgs == null)
                    return;

                try
                {
                    eventArgs.SetBuffer(buffer, 0, buffer.Length);

                    StartReceive();
                }
                catch (Exception ex)
                {
                    client.CloseSafely(ex);
                }
            }

            private void EndReceive()
            {
                var eventArgs = this.eventArgs;
                if (eventArgs == null)
                    return;

                try
                {
                    if (eventArgs.BytesTransferred == 0)
                    {
                        client.CloseSafely();
                        return;
                    }

                    var buffer = new ArraySegment<byte>(eventArgs.Buffer, 0, eventArgs.BytesTransferred);

                    var result = client.listener?.DataReceived(ref buffer, direction);

                    switch (result.GetValueOrDefault(OperationResult.Continue))
                    {
                        case OperationResult.Continue:
                            eventArgs.SetBuffer(buffer.Array, buffer.Offset, buffer.Count);
                            StartSend();
                            break;

                        case OperationResult.CloseClient:
                            client.CloseSafely();
                            break;

                        default:
                            throw new InvalidOperationException();
                    }
                }
                catch (Exception ex)
                {
                    client.CloseSafely(ex);
                }
            }

            private void StartSend()
            {
                var eventArgs = this.eventArgs;
                if (eventArgs == null)
                    return;

                try
                {
                    if (!target.SendAsync(eventArgs))
                        EndSend();
                }
                catch (Exception ex)
                {
                    client.CloseSafely(ex);
                }
            }

            public void Dispose()
            {
                if (!disposed)
                {
                    var eventArgs = this.eventArgs;
                    this.eventArgs = null;

                    eventArgs?.Dispose();

                    disposed = true;
                }
            }
        }
    }
}
