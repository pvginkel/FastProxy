using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FastProxy
{
    internal class ProxyClient : IDisposable
    {
        private Socket source;
        private Socket target;
        private Channel upstream;
        private Channel downstream;
        private volatile int channelsCompleted;
        private readonly IPEndPoint endpoint;
        private readonly IListener listener;
        private readonly int bufferSize;
        private readonly EventArgsManager eventArgsManager;
        private bool disposed;

        public event ExceptionEventHandler ExceptionOccured;
        public event EventHandler Closed;

        public ProxyClient(Socket client, IPEndPoint endpoint, IListener listener, int bufferSize, EventArgsManager eventArgsManager)
        {
            this.source = client;
            this.endpoint = endpoint;
            this.listener = listener ?? Listeners.Sink;
            this.bufferSize = bufferSize;
            this.eventArgsManager = eventArgsManager;
        }

        public void Start()
        {
            var eventArgs = new SocketAsyncEventArgs();
            eventArgs.Completed += ConnectEventArgs_Completed;

            try
            {
                target = new Socket(endpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                eventArgs.RemoteEndPoint = endpoint;
                if (!target.ConnectAsync(eventArgs))
                    EndConnect(eventArgs);
            }
            catch (Exception ex)
            {
                CloseSafely(ex);
            }
        }

        private void ConnectEventArgs_Completed(object sender, SocketAsyncEventArgs e)
        {
            EndConnect(e);
        }

        private void EndConnect(SocketAsyncEventArgs eventArgs)
        {
            Debug.Assert(eventArgs.ConnectSocket == target);

            if (target.Connected)
            {
                try
                {
                    source.LingerState = new LingerOption(false, 0);
                    target.LingerState = new LingerOption(false, 0);

                    listener.Connected();

                    eventArgs.Dispose();

                    upstream = new Channel(Direction.Upstream, target, source, this);
                    downstream = new Channel(Direction.Downstream, source, target, this);
                }
                catch (Exception ex)
                {
                    CloseSafely(ex);
                }
            }
            else
            {
                CloseSafely();
            }
        }

        private void CloseSafely(Exception exception = null)
        {
            try
            {
                Dispose();
            }
            catch (Exception ex)
            {
                if (exception == null)
                    exception = ex;
            }

            try
            {
                listener.Closed();
            }
            catch (Exception ex)
            {
                if (exception == null)
                    exception = ex;
            }

            OnClosed();

            if (exception != null)
                OnExceptionOccured(new ExceptionEventArgs(exception));
        }

        protected virtual void OnExceptionOccured(ExceptionEventArgs e)
        {
            ExceptionOccured?.Invoke(this, e);
        }

        protected virtual void OnClosed()
        {
            Closed?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (!disposed)
            {
                if (source != null)
                {
                    source.Shutdown(SocketShutdown.Both);
                    source.Dispose();
                    source = null;
                }
                if (target != null)
                {
                    target.Shutdown(SocketShutdown.Both);
                    target.Dispose();
                    target = null;
                }
                if (upstream != null)
                {
                    upstream.Dispose();
                    upstream = null;
                }
                if (downstream != null)
                {
                    downstream.Dispose();
                    downstream = null;
                }

                disposed = true;
            }
        }

        private class Channel : IDisposable
        {
            [Flags]
            private enum State
            {
                None = 0,
                Sending = 1,
                DataReceived = 2,
                ReceiveCompleted = 4,
                Completed = 8
            }

            private readonly Direction direction;
            private readonly Socket source;
            private readonly Socket target;
            private readonly ProxyClient client;
            private EventArgsPair eventArgs;
            private volatile int state;
            private bool disposed;

            public Channel(Direction direction, Socket source, Socket target, ProxyClient client)
            {
                this.direction = direction;
                this.source = source;
                this.target = target;
                this.client = client;

                eventArgs = client.eventArgsManager.Take();
                eventArgs.Send.Completed += SendEventArgs_Completed;
                eventArgs.Receive.Completed += ReceiveEventArgs_Completed;

                StartReceive();
            }

            private void StartReceive()
            {
                var eventArgs = this.eventArgs;
                if (eventArgs == null)
                    return;

                try
                {
                    // The buffer associated with the event args is twice
                    // client.bufferSize. Ever time we start a receive, we
                    // switch between the base offset, and the base offset
                    // offset by client.bufferSize.

                    int offset = eventArgs.Receive.Offset != eventArgs.BufferOffset
                        ? eventArgs.BufferOffset
                        : eventArgs.BufferOffset + client.bufferSize;

                    eventArgs.Receive.SetBuffer(offset, client.bufferSize);

                    if (!source.ReceiveAsyncSuppressFlow(eventArgs.Receive))
                        EndReceive();
                }
                catch (Exception ex)
                {
                    client.CloseSafely(ex);
                }
            }

            private void ReceiveEventArgs_Completed(object sender, SocketAsyncEventArgs e)
            {
                EndReceive();
            }

            private void EndReceive()
            {
                var eventArgs = this.eventArgs;
                if (eventArgs == null)
                    return;

                var receiveCompleted = eventArgs.Receive.BytesTransferred == 0;

                UpdateStateDataReceived(receiveCompleted, out bool completed, out bool sending);
                if (completed)
                    CompleteChannel();
                else if (!sending && !receiveCompleted)
                    DataReceived();
            }

            private void DataReceived()
            {
                var eventArgs = this.eventArgs;
                if (eventArgs == null)
                    return;

                try
                {
                    int offset = eventArgs.Receive.Offset;
                    var bytesTransferred = eventArgs.Receive.BytesTransferred;

                    var result = client.listener.DataReceived(bytesTransferred, direction);

                    switch (result)
                    {
                        case OperationResult.Continue:
                            StartSend(offset, bytesTransferred);
                            StartReceive();
                            break;

                        case OperationResult.CloseClient:
                            // The CloseClient operation result is implemented by pretending that
                            // we've finished receiving data. This should properly close both channels.
                            // If there's an outstanding send, that will pick up the final close.

                            client.upstream.UpdateStateDataReceived(true, out bool completed, out _);
                            if (completed)
                                client.upstream.CompleteChannel();
                            client.downstream.UpdateStateDataReceived(true, out completed, out _);
                            if (completed)
                                client.downstream.CompleteChannel();
                            break;

                        default:
                            throw new InvalidOperationException();
                    }
                }
                catch (Exception ex)
                {
                    client.CloseSafely(ex);
                }
            }

            private void StartSend(int offset, int count)
            {
                var eventArgs = this.eventArgs;
                if (eventArgs == null)
                    return;

                try
                {
                    eventArgs.Send.SetBuffer(offset, count);

                    UpdateStateStartSending();

                    if (!target.SendAsyncSuppressFlow(eventArgs.Send))
                        EndSend();
                }
                catch (Exception ex)
                {
                    client.CloseSafely(ex);
                }
            }

            private void SendEventArgs_Completed(object sender, SocketAsyncEventArgs e)
            {
                EndSend();
            }

            private void EndSend()
            {
                try
                {
                    UpdateStateSendingComplete(out bool dataReceived, out bool completed);
                    if (completed)
                        CompleteChannel();
                    else if (dataReceived)
                        DataReceived();
                }
                catch (Exception ex)
                {
                    client.CloseSafely(ex);
                }
            }

            private void UpdateStateSendingComplete(out bool dataReceived, out bool completed)
            {
                State oldState;
                State newState;

                do
                {
                    oldState = newState = (State)state;
                    Debug.Assert((oldState & State.Sending) != 0);
                    newState &= ~(State.Sending | State.DataReceived);
                    dataReceived = (oldState & State.DataReceived) != 0;
                    bool receiveCompleted = (oldState & State.ReceiveCompleted) != 0;
                    completed = false;
                    if (receiveCompleted)
                    {
                        Debug.Assert((oldState & State.Completed) == 0);
                        newState |= State.Completed;
                        completed = true;
                    }
                }
                while (Interlocked.CompareExchange(ref state, (int)newState, (int)oldState) != (int)oldState);
            }

            private void UpdateStateStartSending()
            {
                State oldState;
                State newState;

                do
                {
                    oldState = newState = (State)state;
                    Debug.Assert((oldState & State.Sending) == 0);
                    newState |= State.Sending;
                }
                while (Interlocked.CompareExchange(ref state, (int)newState, (int)oldState) != (int)oldState);
            }

            private void UpdateStateDataReceived(bool receiveCompleted, out bool completed, out bool sending)
            {
                State oldState;
                State newState;

                do
                {
                    oldState = newState = (State)state;
                    Debug.Assert((oldState & State.DataReceived) == 0);
                    sending = (oldState & State.Sending) != 0;
                    completed = false;
                    if (receiveCompleted)
                    {
                        newState |= State.ReceiveCompleted;
                        if (!sending)
                        {
                            Debug.Assert((oldState & State.Completed) == 0);
                            newState |= State.Completed;
                            completed = true;
                        }
                    }
                    if (sending && !completed)
                        newState |= State.DataReceived;
                }
                while (Interlocked.CompareExchange(ref state, (int)newState, (int)oldState) != (int)oldState);
            }

            private void CompleteChannel()
            {
                int oldChannelsCompleted;
                int newChannelsCompleted;
                int mask = 1 << (int)direction;
                bool wasClosed;
                bool otherClosed;

                do
                {
                    oldChannelsCompleted = newChannelsCompleted = client.channelsCompleted;

                    wasClosed = (oldChannelsCompleted & mask) != 0;
                    otherClosed = (oldChannelsCompleted & ~mask) != 0;

                    newChannelsCompleted |= mask;
                }
                while (Interlocked.CompareExchange(ref client.channelsCompleted, newChannelsCompleted, oldChannelsCompleted) != oldChannelsCompleted);

                if (!wasClosed && otherClosed)
                    client.CloseSafely();
            }

            public void Dispose()
            {
                if (!disposed)
                {
                    var eventArgs = this.eventArgs;
                    this.eventArgs = null;

                    if (eventArgs != null)
                    {
                        eventArgs.Send.Completed -= SendEventArgs_Completed;
                        eventArgs.Receive.Completed -= ReceiveEventArgs_Completed;
                        client.eventArgsManager.Return(eventArgs);
                    }

                    disposed = true;
                }
            }
        }
    }
}
