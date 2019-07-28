using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy
{
    /// <summary>
    /// Defines a value range.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    public class Range<T>
    {
        /// <summary>
        /// Gets the minimum value.
        /// </summary>
        public T Minimum { get; }

        /// <summary>
        /// Gets the maximum value.
        /// </summary>
        public T Maximum { get; }

        /// <summary>
        /// Initializes a new <see cref="Range{T}"/>.
        /// </summary>
        /// <param name="minimum">The minimum value.</param>
        /// <param name="maximum">The maximum value.</param>
        public Range(T minimum, T maximum)
        {
            Minimum = minimum;
            Maximum = maximum;
        }
    }
}
