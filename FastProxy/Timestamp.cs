using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FastProxy
{
    internal struct Timestamp : IEquatable<Timestamp>, IComparable<Timestamp>, IComparable
    {
        public static Timestamp Empty => new Timestamp();
        public static Timestamp Create() => new Timestamp(Stopwatch.GetTimestamp());

        public long Ticks { get; }

        public Timestamp(long ticks)
        {
            this.Ticks = ticks;
        }

        public bool Equals(Timestamp other)
        {
            return Ticks == other.Ticks;
        }

        public override bool Equals(object obj)
        {
            return obj is Timestamp other && Equals(other);
        }

        public override int GetHashCode()
        {
            return Ticks.GetHashCode();
        }

        public int CompareTo(Timestamp other)
        {
            return Ticks.CompareTo(other.Ticks);
        }

        public int CompareTo(object obj)
        {
            if (ReferenceEquals(null, obj))
                return 1;
            if (obj is Timestamp other)
                return CompareTo(other);
            throw new ArgumentException($"Object must be of type {nameof(Timestamp)}");
        }

        public static bool operator ==(Timestamp left, Timestamp right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Timestamp left, Timestamp right)
        {
            return !left.Equals(right);
        }

        public static bool operator <(Timestamp left, Timestamp right)
        {
            return left.CompareTo(right) < 0;
        }

        public static bool operator >(Timestamp left, Timestamp right)
        {
            return left.CompareTo(right) > 0;
        }

        public static bool operator <=(Timestamp left, Timestamp right)
        {
            return left.CompareTo(right) <= 0;
        }

        public static bool operator >=(Timestamp left, Timestamp right)
        {
            return left.CompareTo(right) >= 0;
        }

        public static Timestamp operator +(Timestamp left, TimeSpan right)
        {
            long add = (right.Ticks * TimeSpan.TicksPerSecond) / Stopwatch.Frequency;
            return new Timestamp(left.Ticks + add);
        }

        public static TimeSpan operator -(Timestamp left, Timestamp right)
        {
            long difference = left.Ticks - right.Ticks;
            long ticks = (difference * TimeSpan.TicksPerSecond) / Stopwatch.Frequency;
            return new TimeSpan(ticks);
        }
    }
}
