using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy.App
{
    internal class BulkSocket : IFastSocket
    {
        private Socket socket;
        private SocketAsyncEventArgs sendEventArgs;
        private SocketAsyncEventArgs receiveEventArgs;
        private int blockCount;
        private int remaining;
        private readonly object syncRoot = new object();
        private bool disposed;

        public event EventHandler Completed;
        public event ExceptionEventHandler ExceptionOccured;

        public BulkSocket(Socket socket, int blockSize, int blockCount)
        {
            this.socket = socket;
            this.blockCount = blockCount;
            this.remaining = blockSize * blockCount;

            sendEventArgs = new SocketAsyncEventArgs();
            sendEventArgs.SetBuffer(new byte[blockSize], 0, blockSize);
            sendEventArgs.Completed += (s, e) => EndSend();

            receiveEventArgs = new SocketAsyncEventArgs();
            receiveEventArgs.SetBuffer(new byte[blockSize], 0, blockSize);
            receiveEventArgs.Completed += (s, e) => EndReceive();
        }

        public void Start()
        {
            StartSend();
            StartReceive();
        }

        private void StartSend()
        {
            var eventArgs = sendEventArgs;
            if (eventArgs == null)
                return;

            do
            {
                lock (syncRoot)
                {
                    if (blockCount <= 0)
                    {
                        if (remaining <= 0)
                        {
                            Close();
                            OnCompleted();
                        }
                        return;
                    }

                    blockCount--;
                }
            }
            while (!socket.SendAsyncSuppressFlow(eventArgs));
        }

        private void EndSend()
        {
            StartSend();
        }

        private void StartReceive()
        {
            var eventArgs = receiveEventArgs;
            if (eventArgs == null)
                return;

            if (!socket.ReceiveAsyncSuppressFlow(eventArgs))
                EndReceive();
        }

        private void EndReceive()
        {
            SocketAsyncEventArgs eventArgs;

            do
            {
                eventArgs = receiveEventArgs;
                if (eventArgs == null)
                    return;

                if (eventArgs.BytesTransferred == 0 || eventArgs.SocketError != SocketError.Success)
                {
                    Close();
                    OnCompleted();

                    return;
                }

                lock (syncRoot)
                {
                    remaining -= eventArgs.BytesTransferred;

                    if (remaining <= 0 && blockCount <= 0)
                    {
                        Close();
                        OnCompleted();

                        return;
                    }
                }
            }
            while (!socket.ReceiveAsyncSuppressFlow(eventArgs));
        }

        private void Close()
        {
            try
            {
                Dispose();
            }
            catch (Exception ex)
            {
                OnExceptionOccured(new ExceptionEventArgs(ex));
            }
        }

        private void OnExceptionOccured(ExceptionEventArgs e)
        {
            ExceptionOccured?.Invoke(this, e);
        }

        private void OnCompleted()
        {
            Completed?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (!disposed)
            {
                if (socket != null)
                {
                    socket.Shutdown(SocketShutdown.Both);
                    socket.Dispose();
                    socket = null;
                }
                if (sendEventArgs != null)
                {
                    sendEventArgs.Dispose();
                    sendEventArgs = null;
                }
                if (receiveEventArgs != null)
                {
                    receiveEventArgs.Dispose();
                    receiveEventArgs = null;
                }

                disposed = true;
            }
        }
    }
}
