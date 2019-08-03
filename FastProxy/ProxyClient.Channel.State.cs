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
                None = 0,
                Sending = 1,
                Receiving = 2,
                DataReceived = 4,
                ReceiveCompleted = 8,
                Completed = 16,
                Aborted = 32
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool IsSet(int state, State flag) => (state & (int)flag) != 0;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void Set(ref int state, State flag) => state |= (int)flag;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static void Clear(ref int state, State flag) => state &= ~(int)flag;

            private void UpdateStateStartReceiving(out bool completed, out bool aborted)
            {
                int oldState;
                int newState;

                do
                {
                    oldState = newState = state;

                    Debug.Assert(!IsSet(oldState, State.Receiving));

                    aborted = IsSet(oldState, State.Aborted);
                    completed = false;

                    if (aborted)
                    {
                        completed =
                            !IsSet(oldState, State.Sending) &&
                            !IsSet(oldState, State.Completed);

                        if (completed)
                            Set(ref newState, State.Completed);
                    }
                    else
                    {
                        Set(ref newState, State.Receiving);
                    }
                }
                while (Interlocked.CompareExchange(ref state, newState, oldState) != oldState);
            }

            private void UpdateStateReceivingComplete(bool receiveCompleted, out bool completed, out bool aborted, out bool sending)
            {
                int oldState;
                int newState;

                do
                {
                    oldState = newState = state;

                    Debug.Assert(IsSet(oldState, State.Receiving));

                    Clear(ref newState, State.Receiving);

                    aborted = IsSet(oldState, State.Aborted);
                    sending = IsSet(oldState, State.Sending);
                    completed = false;

                    if (aborted)
                    {
                        completed = !sending && !IsSet(oldState, State.Completed);
                        if (completed)
                            Set(ref newState, State.Completed);
                    }
                    else
                    {
                        if (receiveCompleted || IsSet(oldState, State.DataReceived))
                        {
                            Set(ref newState, State.ReceiveCompleted);

                            completed = !sending && !IsSet(oldState, State.Completed);
                            if (completed)
                                Set(ref newState, State.Completed);
                        }
                        if (sending && !completed)
                            Set(ref newState, State.DataReceived);
                    }
                }
                while (Interlocked.CompareExchange(ref state, newState, oldState) != oldState);
            }

            private void UpdateStateStartSending(out bool completed, out bool aborted)
            {
                int oldState;
                int newState;

                do
                {
                    oldState = newState = state;

                    Debug.Assert(!IsSet(oldState, State.Sending));

                    aborted = IsSet(oldState, State.Aborted);
                    completed = false;

                    if (aborted)
                    {
                        completed =
                            !IsSet(oldState, State.Receiving) &&
                            !IsSet(oldState, State.Completed);

                        if (completed)
                            Set(ref newState, State.Completed);
                    }
                    else
                    {
                        Set(ref newState, State.Sending);
                    }
                }
                while (Interlocked.CompareExchange(ref state, newState, oldState) != oldState);
            }

            private void UpdateStateSendingComplete(out bool dataReceived, out bool completed, out bool aborted)
            {
                int oldState;
                int newState;

                do
                {
                    oldState = newState = state;

                    Debug.Assert(IsSet(oldState, State.Sending));

                    Clear(ref newState, State.Sending);

                    aborted = IsSet(oldState, State.Aborted);
                    dataReceived = false;

                    if (aborted)
                    {
                        completed =
                            !IsSet(oldState, State.Receiving) &&
                            !IsSet(oldState, State.Completed);

                        if (completed)
                            Set(ref newState, State.Completed);
                    }
                    else
                    {
                        Clear(ref newState, State.DataReceived);

                        dataReceived = IsSet(oldState, State.DataReceived);

                        completed =
                            IsSet(oldState, State.ReceiveCompleted) &&
                            !IsSet(oldState, State.Completed);

                        if (completed)
                            Set(ref newState, State.Completed);
                    }
                }
                while (Interlocked.CompareExchange(ref state, newState, oldState) != oldState);
            }

            private void UpdateStateAbort(out bool completed)
            {
                int oldState;
                int newState;

                do
                {
                    oldState = newState = state;

                    Set(ref newState, State.Aborted);

                    completed =
                        !IsSet(oldState, State.Sending) &&
                        !IsSet(oldState, State.Receiving) &&
                        !IsSet(oldState, State.Completed);

                    if (completed)
                        Set(ref newState, State.Completed);
                }
                while (Interlocked.CompareExchange(ref state, newState, oldState) != oldState);
            }
        }
    }
}