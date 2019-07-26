using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy.Listeners
{
    public class SinkListener : IListener
    {
        public static readonly SinkListener Instance = new SinkListener();

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

        public void Dispose()
        {
        }
    }
}
