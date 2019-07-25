using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy.App
{
    internal abstract class FastClient
    {
        private readonly IPEndPoint endpoint;
        private IFastSocket socket;

        public event EventHandler Completed;
        public event ExceptionEventHandler ExceptionOccured;

        protected FastClient(IPEndPoint endpoint)
        {
            this.endpoint = endpoint;
        }

        protected abstract IFastSocket CreateSocket(Socket client);

        public void Start()
        {
            var client = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            client.Connect(endpoint);

            socket = CreateSocket(client);
            socket.ExceptionOccured += (s, e) => OnExceptionOccured(e);
            socket.Start();
        }

        protected virtual void OnCompleted() => Completed?.Invoke(this, EventArgs.Empty);
        protected virtual void OnExceptionOccured(ExceptionEventArgs e) => ExceptionOccured?.Invoke(this, e);

        public void Close()
        {
            socket.Dispose();
        }
    }
}
