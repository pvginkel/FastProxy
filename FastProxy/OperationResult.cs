using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy
{
    public struct OperationResult
    {
        private readonly OperationContinuation continuation;

        public static OperationResult Continue => new OperationResult();
        public static OperationResult CloseClient => new OperationResult(OperationContinuation.CloseClientMarker);

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
