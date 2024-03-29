﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FastProxy
{
    internal static class SocketExtensions
    {
        public static bool SendAsyncSuppressFlow(this Socket self, SocketAsyncEventArgs e)
        {
            var control = ExecutionContext.SuppressFlow();
            try
            {
                return self.SendAsync(e);
            }
            catch (ObjectDisposedException)
            {
                return true;
            }
            finally
            {
                control.Undo();
            }
        }

        public static bool ReceiveAsyncSuppressFlow(this Socket self, SocketAsyncEventArgs e)
        {
            var control = ExecutionContext.SuppressFlow();
            try
            {
                return self.ReceiveAsync(e);
            }
            catch (ObjectDisposedException)
            {
                return true;
            }
            finally
            {
                control.Undo();
            }
        }
    }
}
