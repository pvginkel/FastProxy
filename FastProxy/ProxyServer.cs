using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy
{
    /// <summary>
    /// Provides a simple proxy TCP/IP server.
    /// </summary>
    public class ProxyServer : IDisposable
    {
        /// <summary>
        /// The default backlog.
        /// </summary>
        public const int DefaultBacklog = 16;

        /// <summary>
        /// The default buffer size for data transfer.
        /// </summary>
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

        /// <summary>
        /// Gets the local endpoint of the server.
        /// </summary>
        public IPEndPoint Endpoint => (IPEndPoint)socket.LocalEndPoint;

        /// <summary>
        /// Raised when an exception occurs.
        /// </summary>
        public event ExceptionEventHandler ExceptionOccured;

        /// <summary>
        /// Initializes a new <see cref="ProxyServer"/>.
        /// </summary>
        /// <param name="endpoint">The endpoint to host the proxy server; set the port number to 0 to
        /// select a port at random, retrieved through <see cref="Endpoint"/>.</param>
        /// <param name="connector">The connector used to accept incoming connections.</param>
        /// <param name="backlog">The listener backlog.</param>
        /// <param name="bufferSize">The buffer size for data transfer.</param>
        public ProxyServer(IPEndPoint endpoint, IConnector connector, int backlog = DefaultBacklog, int bufferSize = DefaultBufferSize)
        {
            this.endpoint = endpoint;
            this.connector = connector;
            this.backlog = backlog;
            this.bufferSize = bufferSize;
            this.eventArgsManager = new EventArgsManager(bufferSize);
        }

        /// <summary>
        /// Start listening for incoming connections.
        /// </summary>
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
            var acceptEventArgs = this.acceptEventArgs;
            var socket = this.socket;
            if (acceptEventArgs == null || socket == null)
                return;

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
                    var result = connector.Connect(out var endpoint, out var listener);
                    if (result == ConnectResult.Accept)
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

        /// <summary>
        /// Raises the <see cref="ExceptionOccured"/> event.
        /// </summary>
        /// <param name="e">The event arguments.</param>
        protected virtual void OnExceptionOccured(ExceptionEventArgs e) => ExceptionOccured?.Invoke(this, e);

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

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!disposed)
            {
                DisposeUtils.DisposeSafely(ref socket);
                DisposeUtils.DisposeSafely(ref acceptEventArgs);

                CloseClientsSafely();

                DisposeUtils.DisposeSafely(ref eventArgsManager);

                disposed = true;
            }
        }
    }
}
