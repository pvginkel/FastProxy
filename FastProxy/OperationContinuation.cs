using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FastProxy.Listeners;

namespace FastProxy
{
    /// <summary>
    /// Allows the outcome of a <see cref="OperationResult"/> to be delayed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The purpose of the <see cref="OperationContinuation"/> class is to produce a
    /// <see cref="OperationResult"/> the outcome of which will be decided at a
    /// later time.
    /// </para>
    /// <para>
    /// A use case of this class is the <see cref="ThrottlingListener"/>. If the
    /// budget is exceeded in a specific time frame, it'll return a
    /// <see cref="OperationOutcome.Pending"/> outcome. Then, at the end of the time
    /// slice, it processes all pending outcomes and allows them to continue. This
    /// spreads out data transfer over time.
    /// </para>
    /// </remarks>
    public class OperationContinuation
    {
        //
        // This implementation is optimized for memory usage and to be lock free.
        //
        // The idea is that we have a single field callback that's used to manage the
        // complete state of the continuation.
        //
        // The callback field can be in the following states:
        //
        // * null: The operation continuation has not yet been completed;
        // * ContinueMarker or CloseMarker: An outcome has been assigned, but the
        //   callback wasn't yet assigned;
        // * DoneMarker: The callback was called with the proper outcome;
        // * Anything else: the callback was assigned but we didn't yet have an outcome.
        //
        // This state is managed as follows:
        //
        // * In SetOutcome:
        //
        //   * If the continuation was already set or completed (one of the markers), we fail;
        //   * If the continuation was set (not null and not a marker), we mark it
        //     as completed (DoneMarker) and raise immediately;
        //   * Otherwise, we assign the ContinueMarker or CloseMarker to set the outcome.
        //
        // * In SetCallback:
        //
        //   * If the continuation was completed already (DoneMarker), we fail;
        //   * If the outcome was already set (ContinueMarker or CloseMarker), we mark it as
        //     completed (DoneMarker) and raise the callback we got in the method;
        //   * Otherwise, we assign the callback and let SetOutcome raise it.
        //

        internal static readonly OperationContinuation CloseClientMarker = new OperationContinuation();

        // IMPORTANT! Don't remove these action allocations. We are using the object identity of these
        // markers to manage state. The C# compiler has optimizations to specifically reduce action
        // allocations, which could merge these three into a single one.
        private static readonly Action<OperationOutcome> ContinueMarker = new Action<OperationOutcome>(p => { });
        private static readonly Action<OperationOutcome> CloseMarker = new Action<OperationOutcome>(p => { });
        private static readonly Action<OperationOutcome> DoneMarker = new Action<OperationOutcome>(p => { });

        private volatile Action<OperationOutcome> callback;

        /// <summary>
        /// Gets the <see cref="OperationResult"/> controlled by this <see cref="OperationContinuation"/>.
        /// </summary>
        public OperationResult Result => new OperationResult(this);

        /// <summary>
        /// Sets the outcome of the pending operation.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method can only be called once and requires a value of
        /// <see cref="OperationOutcome.Continue"/> or <see cref="OperationOutcome.CloseClient"/>.
        /// If the callback has already been assigned, it'll be called immediately. Otherwise
        /// the outcome will be stored and used when the callback is assigned.
        /// </para>
        /// </remarks>
        /// <param name="outcome">The outcome of the pending operation.</param>
        public void SetOutcome(OperationOutcome outcome)
        {
            if (outcome != OperationOutcome.Continue && outcome != OperationOutcome.CloseClient)
                throw new ArgumentOutOfRangeException(nameof(outcome));

            Action<OperationOutcome> oldCallback;
            Action<OperationOutcome> newCallback;
            bool raise;
            bool conflict;

            do
            {
                oldCallback = callback;

                raise = false;
                conflict = false;

                if (oldCallback == null)
                {
                    newCallback = outcome == OperationOutcome.Continue
                        ? ContinueMarker
                        : CloseMarker;
                }
                else if (oldCallback == ContinueMarker || oldCallback == CloseMarker || oldCallback == DoneMarker)
                {
                    newCallback = oldCallback;
                    conflict = true;
                }
                else
                {
                    newCallback = DoneMarker;
                    raise = true;
                }
            }
            while (Interlocked.CompareExchange(ref callback, newCallback, oldCallback) != oldCallback);

            if (conflict)
                throw new InvalidOperationException("Operation continuation outcome as already set");

            if (raise)
                oldCallback(outcome);
        }

        /// <summary>
        /// Sets the callback to be called when the outcome is set.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method can only be called once. If the outcome is already set, the
        /// this <see cref="OperationContinuation"/> will be set to completed and the
        /// callback will be called immediately. Otherwise the callback will be stored
        /// and called when the operation does complete.
        /// </para>
        /// </remarks>
        /// <param name="callback">The callback to call when the operation completes.</param>
        public void SetCallback(Action<OperationOutcome> callback)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            Action<OperationOutcome> oldCallback;
            Action<OperationOutcome> newCallback;
            OperationOutcome outcome;
            bool conflict;

            do
            {
                oldCallback = this.callback;

                outcome = OperationOutcome.Pending;
                conflict = false;

                if (oldCallback == ContinueMarker)
                {
                    outcome = OperationOutcome.Continue;
                    newCallback = DoneMarker;
                }
                else if (oldCallback == CloseMarker)
                {
                    outcome = OperationOutcome.CloseClient;
                    newCallback = DoneMarker;
                }
                else if (oldCallback != null)
                {
                    conflict = true;
                    newCallback = oldCallback;
                }
                else
                {
                    newCallback = callback;
                }
            }
            while (Interlocked.CompareExchange(ref this.callback, newCallback, oldCallback) != oldCallback);

            if (conflict)
                throw new InvalidOperationException("Operation continuation callback was already assigned");

            if (outcome != OperationOutcome.Pending)
                callback(outcome);
        }
    }
}
