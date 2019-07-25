using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy
{
    public interface IListener
    {
        void Connected();
        void Closed();
        OperationResult DataReceived(ref ArraySegment<byte> buffer, Direction direction);
    }
}
