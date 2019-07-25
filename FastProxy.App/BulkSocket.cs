using System;
using System.Collections.Generic;
using System.Diagnostics;
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
            sendEventArgs.Completed += SendEventArgs_Completed;

            receiveEventArgs = new SocketAsyncEventArgs();
            receiveEventArgs.SetBuffer(new byte[blockSize], 0, blockSize);
            receiveEventArgs.Completed += ReceiveEventArgs_Completed;
        }

        public void Start()
        {
            StartSend();
            StartReceive();
        }

        private void SendEventArgs_Completed(object sender, SocketAsyncEventArgs e)
        {
            Debug.Assert(e.LastOperation == SocketAsyncOperation.Send);

            EndSend();
        }

        private void StartSend()
        {
            var eventArgs = sendEventArgs;
            if (eventArgs == null)
                return;

            while (true)
            {
                bool send = false;

                lock (syncRoot)
                {
                    if (blockCount <= 0)
                    {
                        if (remaining <= 0)
                        {
                            Close();
                            OnCompleted();
                        }
                    }
                    else
                    {
                        blockCount--;
                        send = true;
                    }
                }

                if (!send)
                    return;
                if (socket.SendAsync(eventArgs))
                    return;
            }
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

            if (!socket.ReceiveAsync(eventArgs))
                EndReceive();
        }

        private void ReceiveEventArgs_Completed(object sender, SocketAsyncEventArgs e)
        {
            Debug.Assert(e.LastOperation == SocketAsyncOperation.Receive);

            EndReceive();
        }

        private void EndReceive()
        {
            while (true)
            {
                var eventArgs = receiveEventArgs;
                if (eventArgs == null)
                    return;

                if (eventArgs.BytesTransferred == 0 || eventArgs.SocketError != SocketError.Success)
                {
                    Close();
                    OnCompleted();
                    return;
                }

                bool receive = true;

                lock (syncRoot)
                {
                    remaining -= eventArgs.BytesTransferred;

                    if (remaining <= 0 && blockCount <= 0)
                    {
                        receive = false;
                        Close();
                        OnCompleted();
                    }
                }

                if (!receive)
                    return;
                if (socket.ReceiveAsync(eventArgs))
                    return;
            }
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
