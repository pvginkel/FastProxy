using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy
{
    internal class BufferManager : IDisposable
    {
        private const int BufferCount = 100;

        private readonly int bufferSize;
        private readonly List<Buffer> buffers = new List<Buffer>();
        private Buffer buffer;
        private bool disposed;

        public BufferManager(int bufferSize)
        {
            if ((bufferSize & (4096 - 1)) != 0)
                throw new ArgumentException("Buffer size must be multiple of 4096", nameof(bufferSize));

            this.bufferSize = bufferSize;
        }

        public ArraySegment<byte> GetBuffer()
        {
            if (buffer == null || buffer.IsFull)
            {
                buffer = new Buffer(bufferSize);
                buffers.Add(buffer);
            }

            return buffer.GetBuffer();
        }

        public void Dispose()
        {
            if (!disposed)
            {
                foreach (var buffer in buffers)
                {
                    buffer.Dispose();
                }
                buffers.Clear();
                buffer = null;

                disposed = true;
            }
        }

        private class Buffer : IDisposable
        {
            private readonly byte[] buffer;
            private readonly int bufferSize;
            private int offset;
            private SocketAsyncEventArgs eventArgs;
            private bool disposed;

            public bool IsFull => buffer.Length - offset < bufferSize;

            public Buffer(int bufferSize)
            {
                this.bufferSize = bufferSize;

                buffer = new byte[BufferCount * bufferSize + 4096];

                // We allocate an event args and set the buffer to make sure that
                // whatever happens when you do this, happens right now. What this
                // does is pin the byte array, to make sure it doesn't move anymore.
                // We do this to ensure that the trick we do below to make sure that
                // we start at a page boundary offset actually works, because
                // pinning the buffer before making the calculations fixes it in
                // the GC.

                eventArgs = new SocketAsyncEventArgs();
                eventArgs.SetBuffer(buffer, 0, 0);

                // Calculate the offset we need to start allocating from. We do this
                // by taking the pointer of the first element of the array, and
                // picking an offset that puts the first allocated buffer at the
                // next page boundary.

                var offset = Marshal.UnsafeAddrOfPinnedArrayElement(buffer, 0);
                ulong pageOffset = (ulong)offset & (4096 - 1);
                this.offset = (int)(4096 - pageOffset);
            }

            public ArraySegment<byte> GetBuffer()
            {
                int offset = this.offset;
                this.offset += bufferSize;
                return new ArraySegment<byte>(buffer, offset, bufferSize);
            }

            public void Dispose()
            {
                if (!disposed)
                {
                    DisposeUtils.DisposeSafely(ref eventArgs);

                    disposed = true;
                }
            }
        }
    }
}
