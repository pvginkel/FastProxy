using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy.TestSupport
{
    public abstract class FastServer
    {
        public const int DefaultBacklog = 16;

        private readonly int backlog;
        private readonly IPEndPoint endpoint;
        private Socket socket;

        public IPEndPoint Endpoint => (IPEndPoint)socket.LocalEndPoint;

        public event ExceptionEventHandler ExceptionOccured;

        protected FastServer(IPEndPoint endpoint, int backlog = DefaultBacklog)
        {
            this.endpoint = endpoint;
            this.backlog = backlog;
        }

        public void Start()
        {
            socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(endpoint);
            socket.Listen(backlog);

            StartAccept(null);
        }

        private void StartAccept(SocketAsyncEventArgs acceptEventArg)
        {
            if (acceptEventArg == null)
            {
                acceptEventArg = new SocketAsyncEventArgs();
                acceptEventArg.Completed += AcceptEventArg_Completed;
            }

            acceptEventArg.AcceptSocket = null;

            if (!socket.AcceptAsync(acceptEventArg))
                ProcessAccept(acceptEventArg);
        }

        private void AcceptEventArg_Completed(object sender, SocketAsyncEventArgs e)
        {
            ProcessAccept(e);
        }

        protected abstract IFastSocket CreateSocket(Socket client);

        private void ProcessAccept(SocketAsyncEventArgs acceptEventArg)
        {
            var client = acceptEventArg.AcceptSocket;

            if (client.Connected)
            {
                try
                {
                    var socket = CreateSocket(client);

                    socket.ExceptionOccured += (s, e) => OnExceptionOccured(e);
                    socket.Start();
                }
                catch (Exception ex)
                {
                    OnExceptionOccured(new ExceptionEventArgs(ex));
                }
            }

            StartAccept(acceptEventArg);
        }

        protected virtual void OnExceptionOccured(ExceptionEventArgs e) => ExceptionOccured?.Invoke(this, e);
    }
}
