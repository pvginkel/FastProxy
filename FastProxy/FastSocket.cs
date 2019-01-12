using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy
{
    public abstract class FastSocket
    {
        public const int DefaultBufferSize = 4096;

        private readonly Socket socket;
        private readonly SocketAsyncEventArgs sendEventArgs;
        private readonly SocketAsyncEventArgs receiveEventArgs;
        private readonly object syncRoot = new object();
        private bool sending;
        private ArraySegment<byte> pendingSend;

        public event ExceptionEventHandler ExceptionOccured;

        protected FastSocket(Socket socket, int bufferSize = DefaultBufferSize)
        {
            this.socket = socket;

            sendEventArgs = new SocketAsyncEventArgs();
            sendEventArgs.Completed += SendEventArgs_Completed;

            receiveEventArgs = new SocketAsyncEventArgs();
            receiveEventArgs.Completed += ReceiveEventArgs_Completed;
            receiveEventArgs.SetBuffer(new byte[bufferSize], 0, bufferSize);
        }

        internal void Start()
        {
            BeginReceive();
        }

        private void BeginReceive()
        {
            if (!socket.ReceiveAsync(receiveEventArgs))
                ProcessReceive();
        }

        private void ProcessReceive()
        {
            if (receiveEventArgs.BytesTransferred == 0 || receiveEventArgs.SocketError != SocketError.Success)
            {
                Close();
                return;
            }

            ProcessRead(new ArraySegment<byte>(receiveEventArgs.Buffer, receiveEventArgs.Offset, receiveEventArgs.BytesTransferred));

            BeginReceive();
        }

        protected abstract void ProcessRead(ArraySegment<byte> buffer);

        public void Close()
        {
            try
            {
                socket.Shutdown(SocketShutdown.Both);
            }
            catch (Exception ex)
            {
                OnExceptionOccured(new ExceptionEventArgs(ex));
            }
        }

        private void SendEventArgs_Completed(object sender, SocketAsyncEventArgs e)
        {
            Debug.Assert(e.LastOperation == SocketAsyncOperation.Send);

            var send = new ArraySegment<byte>();

            lock (syncRoot)
            {
                if (pendingSend.Array != null)
                {
                    send = pendingSend;
                    pendingSend = new ArraySegment<byte>();
                }
                else
                {
                    sending = false;
                }
            }

            if (send.Array != null)
            {
                sendEventArgs.SetBuffer(send.Array, send.Offset, send.Count);

                if (!socket.SendAsync(sendEventArgs))
                    SendComplete();
            }
            else
            {
                SendComplete();
            }
        }

        protected void Send(ArraySegment<byte> buffer)
        {
            lock (syncRoot)
            {
                if (sending)
                {
                    if (pendingSend.Array != null)
                        throw new InvalidOperationException();
                    pendingSend = buffer;
                    return;
                }

                sending = true;
            }

            sendEventArgs.SetBuffer(buffer.Array, buffer.Offset, buffer.Count);

            if (!socket.SendAsync(sendEventArgs))
                SendComplete();
        }

        protected abstract void SendComplete();

        private void ReceiveEventArgs_Completed(object sender, SocketAsyncEventArgs e)
        {
            Debug.Assert(e.LastOperation == SocketAsyncOperation.Receive);

            ProcessReceive();
        }

        protected virtual void OnExceptionOccured(ExceptionEventArgs e)
        {
            ExceptionOccured?.Invoke(this, e);
        }
    }
}
