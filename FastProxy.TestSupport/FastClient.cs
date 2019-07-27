using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy.TestSupport
{
    public abstract class FastClient : IDisposable
    {
        private readonly IPEndPoint endpoint;
        private IFastSocket socket;
        private bool disposed;

        public event EventHandler Completed;
        public event ExceptionEventHandler ExceptionOccured;

        protected FastClient(IPEndPoint endpoint)
        {
            this.endpoint = endpoint;
        }

        protected abstract IFastSocket CreateSocket(Socket client);

        public void Start()
        {
            try
            {
                var client = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                client.Connect(endpoint);

                socket = CreateSocket(client);
                socket.ExceptionOccured += (s, e) => OnExceptionOccured(e);
                socket.Start();
            }
            catch (Exception ex)
            {
                OnExceptionOccured(new ExceptionEventArgs(ex));
            }
        }

        protected virtual void OnCompleted() => Completed?.Invoke(this, EventArgs.Empty);
        protected virtual void OnExceptionOccured(ExceptionEventArgs e) => ExceptionOccured?.Invoke(this, e);

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                DisposeUtils.DisposeSafely(ref socket);

                disposed = true;
            }
        }
    }
}
