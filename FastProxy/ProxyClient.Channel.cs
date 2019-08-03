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