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

        protected override FastSocket CreateSocket(Socket client)
        {
            return new EchoSocket(client);
        }

        private class EchoSocket : FastSocket
        {
            public EchoSocket(Socket socket, int bufferSize = DefaultBufferSize)
                : base(socket, bufferSize)
            {
            }

            protected override void ProcessRead(ArraySegment<byte> buffer)
            {
                //Console.WriteLine("Received on server: " + Encoding.UTF8.GetString(buffer.ToArray()));

                Send(buffer);
            }

            protected override void SendComplete()
            {
                // Nothing to do.
            }
        }
    }
}
