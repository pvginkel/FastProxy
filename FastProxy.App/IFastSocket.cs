using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy.App
{
    internal interface IFastSocket : IDisposable
    {
        event ExceptionEventHandler ExceptionOccured;

        void Start();
    }
}
