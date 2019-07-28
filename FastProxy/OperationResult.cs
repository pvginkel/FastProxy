using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy
{
    /// <summary>
    /// Specifies the outcome of a <see cref="IListener.DataReceived(int, Direction)"/> operation.
    /// </summary>
    public struct OperationResult
    {
        private readonly OperationContinuation continuation;

        /// <summary>
        /// Indicates that the data transfer should continue.
        /// </summary>
        public static OperationResult Continue => new OperationResult();

        /// <summary>
        /// Indicates that the connection should be aborted.
        /// </summary>
        public static OperationResult CloseClient => new OperationResult(OperationContinuation.CloseClientMarker);

        /// <summary>
        /// Gets the outcome of the operation.
        /// </summary>
        public OperationOutcome Outcome
        {
            get
            {
                if (continuation == null)
                    return OperationOutcome.Continue;
                if (continuation == OperationContinuation.CloseClientMarker)
                    return OperationOutcome.CloseClient;
                return OperationOutcome.Pending;
            }
        }

        internal OperationResult(OperationContinuation continuation)
        {
            this.continuation = continuation;
        }

        /// <summary>
        /// Sets the callback to call when a pending operation completes.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Operation results that are created through a <see cref="OperationContinuation"/>
        /// will have their output set at a later time. This method assigns the
        /// callback to be called when that operation does complete.
        /// </para>
        /// <para>
        /// If this operation result is not created through a <see cref="OperationContinuation"/>,
        /// the callback will be called immediately with the actual outcome.
        /// </para>
        /// </remarks>
        /// <param name="callback"></param>
        public void SetCallback(Action<OperationOutcome> callback)
        {
            if (continuation == null)
                callback(OperationOutcome.Continue);
            else if (continuation == OperationContinuation.CloseClientMarker)
                callback(OperationOutcome.CloseClient);
            else
                continuation.SetCallback(callback);
        }
    }
}
