using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy
{
    /// <summary>
    /// Specifies the event arguments for a <see cref="ExceptionEventHandler"/>.
    /// </summary>
    public class ExceptionEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the exception.
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// Initializes a new <see cref="ExceptionEventArgs"/>.
        /// </summary>
        /// <param name="exception">The exception.</param>
        public ExceptionEventArgs(Exception exception)
        {
            Exception = exception;
        }
    }

    /// <summary>
    /// Represents the method raised when an exception is thrown.
    /// </summary>
    /// <param name="sender">The event source.</param>
    /// <param name="e">The event arguments.</param>
    public delegate void ExceptionEventHandler(object sender, ExceptionEventArgs e);
}
