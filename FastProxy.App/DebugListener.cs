using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy.App
{
    public static class DebugListener
    {
        static DebugListener()
        {
#if !NETCOREAPP
            Debug.Listeners.Clear();
            Debug.Listeners.Add(new DebugBreakListener());
#endif
        }

        public static void Setup()
        {
            // Nothing to do.
        }

        private class DebugBreakListener : TraceListener
        {
            [DebuggerNonUserCode]
            public override void Fail(string message, string detailMessage)
            {
                Debugger.Break();
            }

            public override void Write(string message)
            {
                Console.Write(message);
            }

            public override void WriteLine(string message)
            {
                Console.WriteLine(message);
            }
        }
    }
}
