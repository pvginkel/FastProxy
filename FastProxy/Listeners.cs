using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy
{
    public static class Listeners
    {
        public static IListener Sink = new SinkListener();

        private class SinkListener : IListener
        {
            public void Connected()
            {
            }

            public void Closed()
            {
            }

            public OperationResult DataReceived(int bytesTransferred, Direction direction)
            {
                return OperationResult.Continue;
            }
        }
    }
}
