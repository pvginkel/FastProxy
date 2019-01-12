using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy
{
    public abstract class FastClient
    {
        private readonly IPEndPoint endpoint;
        private FastSocket socket;

        public event ExceptionEventHandler ExceptionOccured;

        protected FastClient(IPEndPoint endpoint)
        {
            this.endpoint = endpoint;
        }

        protected abstract FastSocket CreateSocket(Socket client);

        public void Start()
        {
            var client = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            client.Connect(endpoint);

            socket = CreateSocket(client);
            socket.ExceptionOccured += (s, e) => OnExceptionOccured(e);
            socket.Start();
        }

        protected virtual void OnExceptionOccured(ExceptionEventArgs e)
        {
            ExceptionOccured?.Invoke(this, e);
        }

        public void Close()
        {
            socket.Close();
        }
    }
}
