using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FastProxy.Listeners;

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
        private volatile bool aborted;
        private bool disposed;

        public event ExceptionEventHandler ExceptionOccured;
        public event EventHandler Closed;

        public ProxyClient(Socket client, IPEndPoint endpoint, IListener listener, int bufferSize, EventArgsManager eventArgsManager)
        {
            this.source = client;
            this.endpoint = endpoint;
            this.listener = listener ?? SinkListener.Instance;
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

                    upstream.Start();
                    downstream.Start();
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

        protected virtual void OnExceptionOccured(ExceptionEventArgs e) => ExceptionOccured?.Invoke(this, e);
        protected virtual void OnClosed() => Closed?.Invoke(this, EventArgs.Empty);

        public void Dispose()
        {
            if (!disposed)
            {
                if (!aborted)
                {
                    source?.Shutdown(SocketShutdown.Both);
                    target?.Shutdown(SocketShutdown.Both);
                }

                DisposeUtils.DisposeSafely(ref source);
                DisposeUtils.DisposeSafely(ref target);
                DisposeUtils.DisposeSafely(ref upstream);
                DisposeUtils.DisposeSafely(ref downstream);

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
                Receiving = 2,
                DataReceived = 4,
                ReceiveCompleted = 8,
                Completed = 16,
                Aborted = 32
            }

            private readonly Direction direction;
            private readonly Socket source;
            private readonly Socket target;
            private readonly ProxyClient client;
            private EventArgsPair eventArgs;
            private volatile int state;
            private bool disposed;
            private Action<OperationOutcome> operationCallback;

            public Channel(Direction direction, Socket source, Socket target, ProxyClient client)
            {
                this.direction = direction;
                this.source = source;
                this.target = target;
                this.client = client;

                eventArgs = client.eventArgsManager.Take();
                eventArgs.Send.Completed += SendEventArgs_Completed;
                eventArgs.Receive.Completed += ReceiveEventArgs_Completed;
            }

            public void Start()
            {
                StartReceive();
            }

            private void StartReceive()
            {
                while (true)
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

                        UpdateStateStartReceiving(out bool completed, out bool aborted);

                        if (aborted)
                        {
                            if (completed)
                                CompleteChannel();
                            return;
                        }

                        if (!source.ReceiveAsyncSuppressFlow(eventArgs.Receive))
                        {
                            bool again = EndReceive(true);
                            if (again)
                                continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        client.CloseSafely(ex);
                    }

                    break;
                }
            }

            private void ReceiveEventArgs_Completed(object sender, SocketAsyncEventArgs e)
            {
                EndReceive();
            }

            private bool EndReceive(bool receiving = false)
            {
                var eventArgs = this.eventArgs;
                if (eventArgs == null)
                    return false;

                var receiveCompleted = eventArgs.Receive.BytesTransferred == 0;

                UpdateStateReceivingComplete(receiveCompleted, out bool completed, out bool aborted, out bool sending);

                if (completed)
                    CompleteChannel();
                else if (!aborted && !sending && !receiveCompleted)
                    return DataReceived(receiving);

                return false;
            }

            private bool DataReceived(bool receiving = false)
            {
                var eventArgs = this.eventArgs;
                if (eventArgs == null)
                    return false;

                try
                {
                    int offset = eventArgs.Receive.Offset;
                    var bytesTransferred = eventArgs.Receive.BytesTransferred;

                    var result = client.listener.DataReceived(bytesTransferred, direction);
                    var outcome = result.Outcome;

                    if (outcome == OperationOutcome.Pending)
                    {
                        var callback = operationCallback;
                        if (callback == null)
                        {
                            callback = OperationCallback;
                            operationCallback = callback;
                        }

                        result.SetCallback(callback);
                    }
                    else
                    {
                        return CompleteOperation(outcome, offset, bytesTransferred, receiving);
                    }
                }
                catch (Exception ex)
                {
                    client.CloseSafely(ex);
                }

                return false;
            }

            private void OperationCallback(OperationOutcome outcome)
            {
                var eventArgs = this.eventArgs;
                if (eventArgs == null)
                    return;

                try
                {
                    int offset = eventArgs.Receive.Offset;
                    var bytesTransferred = eventArgs.Receive.BytesTransferred;

                    CompleteOperation(outcome, offset, bytesTransferred);
                }
                catch (Exception ex)
                {
                    client.CloseSafely(ex);
                }
            }

            private bool CompleteOperation(OperationOutcome outcome, int offset, int bytesTransferred, bool receiving = false)
            {
                switch (outcome)
                {
                    case OperationOutcome.Continue:
                        StartSend(offset, bytesTransferred);
                        if (receiving)
                            return true;
                        StartReceive();
                        break;

                    case OperationOutcome.CloseClient:
                        client.aborted = true;

                        // The CloseClient operation result is implemented by pretending that
                        // we've finished receiving data. This should properly close both channels.
                        // If there's an outstanding send, that will pick up the final close.

                        AbortChannel(client.upstream);
                        AbortChannel(client.downstream);
                        break;

                    default:
                        throw new InvalidOperationException();
                }

                return false;

                void AbortChannel(Channel channel)
                {
                    if (channel == null)
                        return;

                    channel.UpdateStateAbort(out bool completed);
                    if (completed)
                        channel.CompleteChannel();
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

                    UpdateStateStartSending(out bool completed, out bool aborted);

                    if (aborted)
                    {
                        if (completed)
                            CompleteChannel();
                        return;
                    }

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
                    UpdateStateSendingComplete(out bool dataReceived, out bool completed, out bool aborted);

                    if (completed)
                        CompleteChannel();
                    else if (!aborted && dataReceived)
                        DataReceived();
                }
                catch (Exception ex)
                {
                    client.CloseSafely(ex);
                }
            }

            private void UpdateStateStartReceiving(out bool completed, out bool aborted)
            {
                State oldState;
                State newState;

                do
                {
                    oldState = newState = (State)state;

                    Debug.Assert((oldState & State.Receiving) == 0);

                    aborted = (oldState & State.Aborted) != 0;
                    completed = false;

                    if (aborted)
                    {
                        bool sending = (oldState & State.Sending) != 0;
                        completed = !sending && (oldState & State.Completed) == 0;
                        if (completed)
                            newState |= State.Completed;
                    }
                    else
                    {
                        newState |= State.Receiving;
                    }
                }
                while (Interlocked.CompareExchange(ref state, (int)newState, (int)oldState) != (int)oldState);
            }

            private void UpdateStateReceivingComplete(bool receiveCompleted, out bool completed, out bool aborted, out bool sending)
            {
                State oldState;
                State newState;

                do
                {
                    oldState = newState = (State)state;

                    Debug.Assert((oldState & State.Receiving) != 0);

                    newState &= ~State.Receiving;
                    aborted = (oldState & State.Aborted) != 0;
                    sending = (oldState & State.Sending) != 0;
                    completed = false;

                    if (aborted)
                    {
                        completed = !sending && (oldState & State.Completed) == 0;
                        if (completed)
                            newState |= State.Completed;
                    }
                    else
                    {
                        if (receiveCompleted || (oldState & State.DataReceived) != 0)
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
                }
                while (Interlocked.CompareExchange(ref state, (int)newState, (int)oldState) != (int)oldState);
            }

            private void UpdateStateStartSending(out bool completed, out bool aborted)
            {
                State oldState;
                State newState;

                do
                {
                    oldState = newState = (State)state;

                    Debug.Assert((oldState & State.Sending) == 0);

                    aborted = (oldState & State.Aborted) != 0;
                    completed = false;

                    if (aborted)
                    {
                        bool receiving = (oldState & State.Receiving) != 0;
                        completed = !receiving && (oldState & State.Completed) == 0;
                        if (completed)
                            newState |= State.Completed;
                    }
                    else
                    {
                        newState |= State.Sending;
                    }
                }
                while (Interlocked.CompareExchange(ref state, (int)newState, (int)oldState) != (int)oldState);
            }

            private void UpdateStateSendingComplete(out bool dataReceived, out bool completed, out bool aborted)
            {
                State oldState;
                State newState;

                do
                {
                    oldState = newState = (State)state;

                    Debug.Assert((oldState & State.Sending) != 0);

                    newState &= ~State.Sending;
                    aborted = (oldState & State.Aborted) != 0;
                    completed = false;
                    dataReceived = false;

                    if (aborted)
                    {
                        bool receiving = (oldState & State.Receiving) != 0;
                        completed = !receiving && (oldState & State.Completed) == 0;
                        if (completed)
                            newState |= State.Completed;
                    }
                    else
                    {
                        newState &= ~State.DataReceived;
                        dataReceived = (oldState & State.DataReceived) != 0;
                        bool receiveCompleted = (oldState & State.ReceiveCompleted) != 0;
                        if (receiveCompleted)
                        {
                            Debug.Assert((oldState & State.Completed) == 0);
                            newState |= State.Completed;
                            completed = true;
                        }
                    }
                }
                while (Interlocked.CompareExchange(ref state, (int)newState, (int)oldState) != (int)oldState);
            }

            private void UpdateStateAbort(out bool completed)
            {
                State oldState;
                State newState;

                do
                {
                    oldState = newState = (State)state;

                    newState |= State.Aborted;

                    bool sending = (oldState & State.Sending) != 0;
                    bool receiving = (oldState & State.Receiving) != 0;
                    completed = false;

                    if (!(sending || receiving) && (oldState & State.Completed) == 0)
                    {
                        completed = true;
                        newState |= State.Completed;
                    }
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
