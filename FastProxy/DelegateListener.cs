using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy
{
    public class DelegateListener : IListener
    {
        private readonly IListener parent;

        public DelegateListener(IListener parent)
        {
            this.parent = parent;
        }

        public virtual void Connected()
        {
            parent.Connected();
        }

        public virtual void Closed()
        {
            parent.Closed();
        }

        public virtual OperationResult DataReceived(ref ArraySegment<byte> buffer, Direction direction)
        {
            return parent.DataReceived(ref buffer, direction);
        }
    }
}
