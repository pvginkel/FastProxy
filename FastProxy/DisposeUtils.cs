using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy
{
    internal static class DisposeUtils
    {
        public static void DisposeSafely<T>(ref T disposable)
            where T : IDisposable
        {
            var copy = disposable;
            disposable = default(T);

            copy?.Dispose();
        }
    }
}
