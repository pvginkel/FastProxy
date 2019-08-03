using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FastProxy
{
    internal partial class ProxyClient
    {
        private partial class Channel
        {
            [Flags]
            private enum State
            {
                Sending = 1,
                Receiving = 2,
                DataAvailable = 4,
                Closing = 8,
                Closed = 16
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool IsSet(int state, State flag) => (state & (int)flag) != 0;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void Set(ref int state, State flag) => state |= (int)flag;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void Clear(ref int state, State flag) => state &= ~(int)flag;

            private void UpdateStateStartReceiving(out bool closing, out bool complete)
            {
                int oldState;
                int newState;

                do
                {
                    oldState = newState = state;

                    Debug.Assert(!IsSet(oldState, State.Receiving));
                    Debug.Assert(!IsSet(oldState, State.DataAvailable));
                    Debug.Assert(!IsSet(oldState, State.Closed) || IsSet(oldState, State.Closing));

                    closing = IsSet(oldState, State.Closing);
                    complete = false;

                    if (closing)
                    {
                        complete = !IsSet(oldState, State.Sending | State.Closed);
                        if (complete)
                            Set(ref newState, State.Closed);
                    }
                    else
                    {
                        Set(ref newState, State.Receiving);
                    }
                }
                while (Interlocked.CompareExchange(ref state, newState, oldState) != oldState);
            }

            private void UpdateStateReceivingComplete(bool close, out bool sending, out bool closing, out bool complete)
            {
                int oldState;
                int newState;

                do
                {
                    oldState = newState = state;

                    Debug.Assert(IsSet(oldState, State.Receiving));
                    Debug.Assert(!IsSet(oldState, State.DataAvailable));
                    Debug.Assert(!IsSet(oldState, State.Closed) || IsSet(oldState, State.Closing));

                    Clear(ref newState, State.Receiving);
                    if (close)
                        Set(ref newState, State.Closing);

                    closing = IsSet(oldState, State.Closing) || close;
                    sending = IsSet(oldState, State.Sending);
                    complete = false;

                    if (closing)
                    {
                        complete = !IsSet(oldState, State.Sending | State.Closed);
                        if (complete)
                            Set(ref newState, State.Closed);
                    }
                    else
                    {
                        if (sending)
                            Set(ref newState, State.DataAvailable);
                    }
                }
                while (Interlocked.CompareExchange(ref state, newState, oldState) != oldState);
            }

            private void UpdateStateStartSending(out bool closing, out bool complete)
            {
                int oldState;
                int newState;

                do
                {
                    oldState = newState = state;

                    Debug.Assert(!IsSet(oldState, State.Sending));
                    Debug.Assert(!IsSet(oldState, State.Closed) || IsSet(oldState, State.Closing));

                    closing = IsSet(oldState, State.Closing);
                    complete = false;

                    if (closing)
                    {
                        complete = !IsSet(oldState, State.Receiving | State.Closed);
                        if (complete)
                            Set(ref newState, State.Closed);
                    }
                    else
                    {
                        Set(ref newState, State.Sending);
                    }
                }
                while (Interlocked.CompareExchange(ref state, newState, oldState) != oldState);
            }

            private void UpdateStateSendingComplete(out bool dataAvailable, out bool closing, out bool complete)
            {
                int oldState;
                int newState;

                do
                {
                    oldState = newState = state;

                    Debug.Assert(IsSet(oldState, State.Sending));
                    Debug.Assert(!IsSet(oldState, State.Closed) || IsSet(oldState, State.Closing));

                    Clear(ref newState, State.Sending);

                    closing = IsSet(oldState, State.Closing);
                    dataAvailable = false;
                    complete = false;

                    if (closing)
                    {
                        complete = !IsSet(oldState, State.Receiving | State.Closed);
                        if (complete)
                            Set(ref newState, State.Closed);
                    }
                    else
                    {
                        dataAvailable = IsSet(oldState, State.DataAvailable);

                        Clear(ref newState, State.DataAvailable);
                    }
                }
                while (Interlocked.CompareExchange(ref state, newState, oldState) != oldState);
            }

            private void UpdateStateClosing(out bool complete)
            {
                int oldState;
                int newState;

                do
                {
                    oldState = newState = state;

                    Set(ref newState, State.Closing);

                    complete = !IsSet(oldState, State.Sending | State.Receiving | State.Closed);
                    if (complete)
                        Set(ref newState, State.Closed);
                }
                while (Interlocked.CompareExchange(ref state, newState, oldState) != oldState);
            }
        }
    }
}