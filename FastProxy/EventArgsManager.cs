using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy
{
    internal class EventArgsManager : IDisposable
    {
        private readonly int bufferSize;
        private BufferManager bufferManager;
        private readonly Stack<EventArgsPair> pool = new Stack<EventArgsPair>();
        private readonly object syncRoot = new object();
        private bool disposed;

        public EventArgsManager(int bufferSize)
        {
            this.bufferSize = bufferSize;

            // The buffer size we set in the buffer manager is twice the size
            // we got passed. The reason for this is that the send and receive
            // event args share the same buffer, but read and write from different
            // offsets at a time.
            bufferManager = new BufferManager(bufferSize * 2);
        }

        public EventArgsPair Take()
        {
            ArraySegment<byte> buffer;

            lock (syncRoot)
            {
                if (pool.Count > 0)
                    return pool.Pop();

                buffer = bufferManager.GetBuffer();
            }

            var sendEventArgs = new SocketAsyncEventArgs();
            sendEventArgs.SetBuffer(buffer.Array, buffer.Offset, buffer.Count);
            var receiveEventArgs = new SocketAsyncEventArgs();
            receiveEventArgs.SetBuffer(buffer.Array, buffer.Offset, buffer.Count);

            return new EventArgsPair(
                buffer.Offset,
                sendEventArgs,
                receiveEventArgs
            );
        }

        public void Return(EventArgsPair eventArgs)
        {
            lock (syncRoot)
            {
                eventArgs.Send.SetBuffer(eventArgs.BufferOffset, bufferSize * 2);
                eventArgs.Receive.SetBuffer(eventArgs.BufferOffset, bufferSize * 2);
                pool.Push(eventArgs);
            }
        }

        public void Dispose()
        {
            if (!disposed)
            {
                lock (syncRoot)
                {
                    foreach (var eventArgs in pool)
                    {
                        eventArgs.Send.Dispose();
                        eventArgs.Receive.Dispose();
                    }
                    pool.Clear();

                    if (bufferManager != null)
                    {
                        bufferManager.Dispose();
                        bufferManager = null;
                    }
                }

                disposed = true;
            }
        }
    }
}
