using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FastProxy.Listeners
{
    public class TransferredListener : DelegatingListener
    {
        private long upstream;
        private long downstream;

        public long Upstream => Volatile.Read(ref upstream);
        public long Downstream => Volatile.Read(ref downstream);

        public TransferredListener(IListener inner)
            : base(inner)
        {
        }

        public override OperationResult DataReceived(int bytesTransferred, Direction direction)
        {
            if (direction == Direction.Upstream)
                Interlocked.Add(ref upstream, bytesTransferred);
            else
                Interlocked.Add(ref downstream, bytesTransferred);

            return base.DataReceived(bytesTransferred, direction);
        }
    }
}
