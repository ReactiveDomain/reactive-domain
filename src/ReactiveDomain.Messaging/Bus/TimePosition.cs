namespace ReactiveDomain.Messaging.Bus;

public class TimePosition : IEquatable<TimePosition>, IComparable<TimePosition> {
	private readonly long _value;
	public static readonly TimePosition StartPosition = new(0L);

	public TimePosition(long value) {
		ArgumentOutOfRangeException.ThrowIfNegative(value);
		_value = value;
	}
	public TimeSpan DistanceUntil(TimePosition other) {
		if (this >= other) { return TimeSpan.Zero; }
		return new TimeSpan((other._value - _value) * TimeSpan.TicksPerMillisecond);
	}
	public static bool operator <=(TimePosition a, TimePosition b) {
		ArgumentNullException.ThrowIfNull(a);
		ArgumentNullException.ThrowIfNull(b);
		if (a == b)
			return true;
		return a < b;
	}

	public static bool operator >=(TimePosition a, TimePosition b) {
		ArgumentNullException.ThrowIfNull(a);
		ArgumentNullException.ThrowIfNull(b);
		if (a == b)
			return true;
		return a > b;
	}
	public int CompareTo(TimePosition? other) {
		return (int)(_value - other?._value ?? -1);//-1 is less than StartPosition
	}

	public static bool operator <(TimePosition a, TimePosition b) {
		ArgumentNullException.ThrowIfNull(a);
		ArgumentNullException.ThrowIfNull(b);
		return a._value < b._value;
	}

	public static bool operator >(TimePosition a, TimePosition b) {
		ArgumentNullException.ThrowIfNull(a);
		ArgumentNullException.ThrowIfNull(b);
		return a._value > b._value;
	}

	public static TimePosition operator +(TimePosition a, TimeSpan b) {
		ArgumentNullException.ThrowIfNull(a);
		return new TimePosition(a._value + (long)b.TotalMilliseconds);
	}

	public static TimePosition operator -(TimePosition a, TimeSpan b) {
		ArgumentNullException.ThrowIfNull(a);
		var pos = a._value - (long)b.TotalMilliseconds;
		return pos < 0 ? StartPosition : new TimePosition(pos);
	}
	public static bool operator !=(TimePosition? self, TimePosition? other) => !Equals(self, other);
	public static bool operator ==(TimePosition? self, TimePosition? other) => Equals(self, other);
	public override bool Equals(object? other) => Equals(other as TimePosition);
	public bool Equals(TimePosition? other) {
		return other is not null &&
			   // Value object comparisons
			   _value == other._value;
	}
	public override int GetHashCode() => _value.GetHashCode();
}
