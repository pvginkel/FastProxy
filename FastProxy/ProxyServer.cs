using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy
{
    public class ProxyServer : IDisposable
    {
        public const int DefaultBacklog = 16;
        public const int DefaultBufferSize = 4096;

        private readonly IPEndPoint endpoint;
        private readonly IConnector connector;
        private readonly int backlog;
        private readonly int bufferSize;
        private Socket socket;
        private SocketAsyncEventArgs acceptEventArgs;
        private readonly HashSet<ProxyClient> clients = new HashSet<ProxyClient>();
        private readonly object syncRoot = new object();
        private EventArgsManager eventArgsManager;
        private bool disposed;

        public IPEndPoint Endpoint => (IPEndPoint)socket.LocalEndPoint;

        public event ExceptionEventHandler ExceptionOccured;

        public ProxyServer(IPEndPoint endpoint, IConnector connector, int backlog = DefaultBacklog, int bufferSize = DefaultBufferSize)
        {
            this.endpoint = endpoint;
            this.connector = connector;
            this.backlog = backlog;
            this.bufferSize = bufferSize;
            this.eventArgsManager = new EventArgsManager(bufferSize);
        }

        public void Start()
        {
            acceptEventArgs = new SocketAsyncEventArgs();
            acceptEventArgs.Completed += AcceptEventArgs_Completed;

            socket = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(endpoint);
            socket.Listen(backlog);

            StartAccept();
        }

        private void StartAccept()
        {
            acceptEventArgs.AcceptSocket = null;

            try
            {
                if (!socket.AcceptAsync(acceptEventArgs))
                    EndAccept();
            }
            catch (ObjectDisposedException)
            {
                // We're disposing. Ignore.
            }
        }

        private void AcceptEventArgs_Completed(object sender, SocketAsyncEventArgs e)
        {
            EndAccept();
        }

        private void EndAccept()
        {
            var eventArgs = acceptEventArgs;
            if (eventArgs == null)
                return;

            var client = eventArgs.AcceptSocket;

            if (client.Connected)
            {
                try
                {
                    if (connector.Connect(out var endpoint, out var listener))
                    {
                        var proxyClient = new ProxyClient(client, endpoint, listener, bufferSize, eventArgsManager);

                        lock (syncRoot)
                        {
                            clients.Add(proxyClient);
                        }

                        proxyClient.ExceptionOccured += (s, e) => OnExceptionOccured(e);
                        proxyClient.Closed += ProxyClient_Closed;
                        proxyClient.Start();
                    }
                    else
                    {
                        client.Shutdown(SocketShutdown.Both);
                        client.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    OnExceptionOccured(new ExceptionEventArgs(ex));
                }
            }

            StartAccept();
        }

        private void ProxyClient_Closed(object sender, EventArgs e)
        {
            lock (syncRoot)
            {
                clients.Remove((ProxyClient)sender);
            }
        }

        protected virtual void OnExceptionOccured(ExceptionEventArgs e)
        {
            ExceptionOccured?.Invoke(this, e);
        }

        private void CloseClientsSafely()
        {
            List<ProxyClient> clients;

            lock (syncRoot)
            {
                clients = new List<ProxyClient>(this.clients);
                this.clients.Clear();
            }

            foreach (var client in clients)
            {
                try
                {
                    client.Dispose();
                }
                catch (Exception ex)
                {
                    OnExceptionOccured(new ExceptionEventArgs(ex));
                }
            }
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
                if (acceptEventArgs != null)
                {
                    acceptEventArgs.Dispose();
                    acceptEventArgs = null;
                }

                CloseClientsSafely();

                if (eventArgsManager != null)
                {
                    eventArgsManager.Dispose();
                    eventArgsManager = null;
                }

                disposed = true;
            }
        }
    }
}
