using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy
{
    internal class EventArgsPair
    {
        public int BufferOffset { get; }
        public SocketAsyncEventArgs Send { get; }
        public SocketAsyncEventArgs Receive { get; }

        public EventArgsPair(int bufferOffset, SocketAsyncEventArgs send, SocketAsyncEventArgs receive)
        {
            BufferOffset = bufferOffset;
            Send = send;
            Receive = receive;
        }
    }
}
