using System;

namespace ReactiveDomain.Messaging.Bus {
    public class TimePosition : IEquatable<TimePosition>, IComparable<TimePosition> {
        private readonly long _value;
        public static TimePosition StartPosition = new TimePosition(0L);

        public TimePosition(long value) {
            if (value < 0)
                throw new ArgumentOutOfRangeException();
            _value = value;
        }
        public TimeSpan DistanceUntil(TimePosition other) {
            if (this >= other) { return TimeSpan.Zero; }
            return new TimeSpan((other._value - _value) * TimeSpan.TicksPerMillisecond);
        }
        public static bool operator <=(TimePosition a, TimePosition b) {
            if (a is null || b is null) throw new ArgumentNullException();
            if (a == b) return true;
            return a < b;
        }

        public static bool operator >=(TimePosition a, TimePosition b) {
            if (a is null || b is null) throw new ArgumentNullException();
            if (a == b) return true;
            return a > b;
        }
        public int CompareTo(TimePosition other) {
            return (int)(_value - other?._value ?? -1);//-1 is les than StartPosition
        }

        public static bool operator <(TimePosition a, TimePosition b) {
            if (a is null || b is null) throw new ArgumentNullException();
            return a._value < b._value;
        }

        public static bool operator >(TimePosition a, TimePosition b) {
            if (a is null || b is null) throw new ArgumentNullException();
            return a._value > b._value;
        }

        public static TimePosition operator +(TimePosition a, TimeSpan b) {
            if (a is null) throw new ArgumentNullException();
            return new TimePosition(a._value + (long)b.TotalMilliseconds);
        }

        public static TimePosition operator -(TimePosition a, TimeSpan b) {
            if (a is null) throw new ArgumentNullException();
            var pos = a._value - (long)b.TotalMilliseconds;
            return pos < 0 ? StartPosition : new TimePosition(pos);
        }
        public static bool operator !=(TimePosition self, TimePosition other) => !Equals(self, other);
        public static bool operator ==(TimePosition self, TimePosition other) => Equals(self, other);
        public override bool Equals(object other) => Equals(other as TimePosition);
        public bool Equals(TimePosition other) {
            return !(other is null) &&
                   // Value object comparisons
                   _value == other._value;
        }
        public override int GetHashCode() => _value.GetHashCode();
    }
}