using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using FastProxy.Listeners;

namespace FastProxy
{
    internal partial class ProxyClient : IDisposable
    {
        private Socket source;
        private Socket target;
        private Channel upstream;
        private Channel downstream;
        private volatile int channelsCompleted;
        private readonly IPEndPoint endpoint;
        private readonly IListener listener;
        private readonly int bufferSize;
        private readonly EventArgsManager eventArgsManager;
        private volatile bool aborted;
        private bool disposed;

        public event ExceptionEventHandler ExceptionOccured;
        public event EventHandler Closed;

        public ProxyClient(Socket client, IPEndPoint endpoint, IListener listener, int bufferSize, EventArgsManager eventArgsManager)
        {
            this.source = client;
            this.endpoint = endpoint;
            this.listener = listener ?? SinkListener.Instance;
            this.bufferSize = bufferSize;
            this.eventArgsManager = eventArgsManager;
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
                RaiseException(ex);
                CloseSafely();
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

                    eventArgs.Dispose();

                    upstream = new Channel(Direction.Upstream, target, source, this);
                    downstream = new Channel(Direction.Downstream, source, target, this);

                    upstream.Start();
                    downstream.Start();
                }
                catch (Exception ex)
                {
                    Abort(ex);
                }
            }
            else
            {
                CloseSafely();
            }
        }

        private void CloseSafely()
        {
            try
            {
                Dispose();
            }
            catch (Exception ex)
            {
                RaiseException(ex);
            }

            try
            {
                listener.Closed();
            }
            catch (Exception ex)
            {
                RaiseException(ex);
            }

            OnClosed();
        }

        private void Abort(Exception exception = null)
        {
            if (exception != null)
                RaiseException(exception);

            aborted = true;

            upstream?.Abort();
            downstream?.Abort();

            DisposeUtils.DisposeSafely(ref source);
            DisposeUtils.DisposeSafely(ref target);
        }

        private void RaiseException(Exception exception)
        {
            OnExceptionOccured(new ExceptionEventArgs(exception));
        }

        protected virtual void OnExceptionOccured(ExceptionEventArgs e) => ExceptionOccured?.Invoke(this, e);
        protected virtual void OnClosed() => Closed?.Invoke(this, EventArgs.Empty);

        public void Dispose()
        {
            if (!disposed)
            {
                if (!aborted)
                {
                    source?.Shutdown(SocketShutdown.Both);
                    target?.Shutdown(SocketShutdown.Both);
                }

                DisposeUtils.DisposeSafely(ref source);
                DisposeUtils.DisposeSafely(ref target);
                DisposeUtils.DisposeSafely(ref upstream);
                DisposeUtils.DisposeSafely(ref downstream);

                disposed = true;
            }
        }
    }
}
