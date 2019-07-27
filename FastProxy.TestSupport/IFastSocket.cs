using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy.TestSupport
{
    public interface IFastSocket : IDisposable
    {
        event ExceptionEventHandler ExceptionOccured;

        void Start();
    }
}
