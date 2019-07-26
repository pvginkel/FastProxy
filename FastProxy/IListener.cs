using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy
{
    public interface IListener : IDisposable
    {
        void Connected();
        void Closed();
        OperationResult DataReceived(int bytesTransferred, Direction direction);
    }
}
