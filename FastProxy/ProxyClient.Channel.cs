using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FastProxy
{
    internal partial class ProxyClient
    {
        private partial class Channel : IDisposable
        {
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

                        UpdateStateStartReceiving(out bool closing, out bool complete);

                        if (closing)
                        {
                            if (complete)
                                CompleteChannel();
                            return;
                        }

                        if (!source.ReceiveAsyncSuppressFlow(eventArgs.Receive))
                        {
                            if (EndReceive(true) == ReceiveResult.Again)
                                continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        client.Abort(ex);
                    }

                    break;
                }
            }

            private void ReceiveEventArgs_Completed(object sender, SocketAsyncEventArgs e)
            {
                EndReceive();
            }

            private ReceiveResult EndReceive(bool receiving = false)
            {
                var eventArgs = this.eventArgs;
                if (eventArgs == null)
                    return ReceiveResult.Done;

                var socketError = eventArgs.Receive.SocketError;
                if (socketError != SocketError.Success)
                {
                    client.Abort(CreateSocketErrorException(socketError));
                    return ReceiveResult.Done;
                }

                var close = eventArgs.Receive.BytesTransferred == 0;

                UpdateStateReceivingComplete(close, out bool sending, out bool closing, out bool complete);

                if (closing)
                {
                    if (complete)
                        CompleteChannel();
                    return ReceiveResult.Done;
                }

                if (!sending && !close)
                    return DataAvailable(receiving);

                return ReceiveResult.Done;
            }

            private ReceiveResult DataAvailable(bool receiving = false)
            {
                var eventArgs = this.eventArgs;
                if (eventArgs == null)
                    return ReceiveResult.Done;

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
                    client.Abort(ex);
                }

                return ReceiveResult.Done;
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
                    client.Abort(ex);
                }
            }

            private ReceiveResult CompleteOperation(OperationOutcome outcome, int offset, int bytesTransferred, bool receiving = false)
            {
                switch (outcome)
                {
                    case OperationOutcome.Continue:
                        StartSend(offset, bytesTransferred);
                        if (receiving)
                            return ReceiveResult.Again;
                        StartReceive();
                        break;

                    case OperationOutcome.CloseClient:
                        client.Abort();
                        break;

                    default:
                        throw new InvalidOperationException();
                }

                return ReceiveResult.Done;
            }

            public void Abort()
            {
                UpdateStateClosing(out bool completed);
                if (completed)
                    CompleteChannel();
            }

            private void StartSend(int offset, int count)
            {
                var eventArgs = this.eventArgs;
                if (eventArgs == null)
                    return;

                try
                {
                    eventArgs.Send.SetBuffer(offset, count);

                    UpdateStateStartSending(out bool closing, out bool complete);

                    if (closing)
                    {
                        if (complete)
                            CompleteChannel();
                        return;
                    }

                    if (!target.SendAsyncSuppressFlow(eventArgs.Send))
                        EndSend();
                }
                catch (Exception ex)
                {
                    client.Abort(ex);
                }
            }

            private void SendEventArgs_Completed(object sender, SocketAsyncEventArgs e)
            {
                EndSend();
            }

            private void EndSend()
            {
                var eventArgs = this.eventArgs;
                if (eventArgs == null)
                    return;

                var socketError = eventArgs.Send.SocketError;
                if (socketError != SocketError.Success)
                {
                    client.Abort(CreateSocketErrorException(socketError));
                    return;
                }

                try
                {
                    UpdateStateSendingComplete(out bool dataAvailable, out bool closing, out bool complete);

                    if (closing)
                    {
                        if (complete)
                            CompleteChannel();
                        return;
                    }

                    if (dataAvailable)
                        DataAvailable();
                }
                catch (Exception ex)
                {
                    client.Abort(ex);
                }
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

            private Exception CreateSocketErrorException(SocketError socketError)
            {
                if (socketError == SocketError.OperationAborted)
                {
                    Debug.Assert(client.aborted);
                    return null;
                }

                return new SocketException((int)socketError);
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

            private enum ReceiveResult
            {
                Again,
                Done
            }
        }
    }
}