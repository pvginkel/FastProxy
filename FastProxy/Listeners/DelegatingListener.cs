using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy.Listeners
{
    public class DelegatingListener : IListener
    {
        private readonly IListener inner;

        public DelegatingListener(IListener inner)
        {
            this.inner = inner;
        }

        public virtual void Connected()
        {
            inner.Connected();
        }

        public virtual void Closed()
        {
            inner.Closed();
        }

        public virtual OperationResult DataReceived(int bytesTransferred, Direction direction)
        {
            return inner.DataReceived(bytesTransferred, direction);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            inner.Dispose();
        }
    }
}
