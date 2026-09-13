namespace ReactiveDomain;

/// <summary>
/// A structure referring to a potential logical record position in the Store Main transaction file.
/// While this is based on the Event Store implementation, keep in mind not all stores use the prepare position.
/// </summary>
/// <remarks>
/// Ordered as the store orders them, which is unsigned. The components are carried in a signed
/// <see cref="long"/>, so <see cref="End"/>'s -1 is the same bit pattern as the store's largest
/// position — and that is how it round-trips through the connection wrappers. Compared as signed it
/// would sort below <see cref="Start"/>, putting the end of the log before the beginning of it.
/// </remarks>
public struct Position : IEquatable<Position>, IComparable<Position>, IComparable {
	/// <summary>
	/// Position representing the start of the transaction file
	/// </summary>
	public static readonly Position Start = new(0L, 0L);
	/// <summary>Position representing the end of the transaction file</summary>
	public static readonly Position End = new(-1L, -1L);
	/// <summary>The commit position of the record</summary>
	public readonly long CommitPosition;
	/// <summary>The prepare position of the record.</summary>
	public readonly long PreparePosition;

	/// <summary>
	/// Constructs a position with the given commit and prepare positions.
	/// It is not guaranteed that the position is actually the start of a
	/// record in the transaction file.
	/// 
	/// The commit position cannot be less than the prepare position.
	/// </summary>
	/// <param name="commitPosition">The commit position of the record.</param>
	/// <param name="preparePosition">The prepare position of the record.</param>
	public Position(long commitPosition, long preparePosition) {
		if (commitPosition < preparePosition)
			throw new ArgumentException("The commit position cannot be less than the prepare position", nameof(commitPosition));
		CommitPosition = commitPosition;
		PreparePosition = preparePosition;
	}

	/// <summary>
	/// Orders two positions, unsigned, commit position first. See the note on <see cref="Position"/>
	/// for why signed comparison puts <see cref="End"/> in the wrong place.
	/// </summary>
	private static int Order(Position p1, Position p2) {
		var commit = ((ulong)p1.CommitPosition).CompareTo((ulong)p2.CommitPosition);
		return commit != 0
			? commit
			: ((ulong)p1.PreparePosition).CompareTo((ulong)p2.PreparePosition);
	}

	/// <summary>Compares whether p1 &lt; p2.</summary>
	/// <param name="p1">A <see cref="Position" />.</param>
	/// <param name="p2">A <see cref="Position" />.</param>
	/// <returns>True if p1 &lt; p2.</returns>
	public static bool operator <(Position p1, Position p2) => Order(p1, p2) < 0;

	/// <summary>Compares whether p1 &gt; p2.</summary>
	/// <param name="p1">A <see cref="Position" />.</param>
	/// <param name="p2">A <see cref="Position" />.</param>
	/// <returns>True if p1 &gt; p2.</returns>
	public static bool operator >(Position p1, Position p2) => Order(p1, p2) > 0;

	/// <summary>Compares whether p1 &gt;= p2.</summary>
	/// <param name="p1">A <see cref="Position" />.</param>
	/// <param name="p2">A <see cref="Position" />.</param>
	/// <returns>True if p1 &gt;= p2.</returns>
	public static bool operator >=(Position p1, Position p2) => Order(p1, p2) >= 0;

	/// <summary>Compares whether p1 &lt;= p2.</summary>
	/// <param name="p1">A <see cref="Position" />.</param>
	/// <param name="p2">A <see cref="Position" />.</param>
	/// <returns>True if p1 &lt;= p2.</returns>
	public static bool operator <=(Position p1, Position p2) => Order(p1, p2) <= 0;

	/// <summary>Orders this position against another, so positions can be sorted.</summary>
	/// <param name="other">The position to compare against.</param>
	/// <returns>Negative, zero or positive as this precedes, matches or follows <paramref name="other"/>.</returns>
	public int CompareTo(Position other) => Order(this, other);

	/// <inheritdoc cref="CompareTo(Position)"/>
	/// <param name="obj">A <see cref="Position"/>, or null, which every position follows.</param>
	/// <exception cref="ArgumentException"><paramref name="obj"/> is not a <see cref="Position"/>.</exception>
	public int CompareTo(object? obj) =>
		obj switch {
			null => 1,
			Position other => Order(this, other),
			_ => throw new ArgumentException($"Cannot compare a {nameof(Position)} to a {obj.GetType().Name}.",
				nameof(obj))
		};

	/// <summary>Compares p1 and p2 for equality.</summary>
	/// <param name="p1">A <see cref="Position" />.</param>
	/// <param name="p2">A <see cref="Position" />.</param>
	/// <returns>True if p1 is equal to p2.</returns>
	public static bool operator ==(Position p1, Position p2) {
		if (p1.CommitPosition == p2.CommitPosition)
			return p1.PreparePosition == p2.PreparePosition;
		return false;
	}

	/// <summary>Compares p1 and p2 for equality.</summary>
	/// <param name="p1">A <see cref="Position" />.</param>
	/// <param name="p2">A <see cref="Position" />.</param>
	/// <returns>True if p1 is not equal to p2.</returns>
	public static bool operator !=(Position p1, Position p2) {
		return !(p1 == p2);
	}

	/// <summary>
	/// Indicates whether this instance and a specified object are equal.
	/// </summary>
	/// <returns>
	/// true if <paramref name="obj" /> and this instance are the same type and represent the same value; otherwise, false.
	/// </returns>
	/// <param name="obj">Another object to compare to. </param>
	/// <filterpriority>2</filterpriority>
	public override bool Equals(object? obj) {
		if (obj is Position position)
			return Equals(position);
		return false;
	}

	/// <summary>
	/// Compares this instance of <see cref="Position" /> for equality
	/// with another instance.
	/// </summary>
	/// <param name="other">A <see cref="Position" /></param>
	/// <returns>True if this instance is equal to the other instance.</returns>
	public bool Equals(Position other) {
		return this == other;
	}

	/// <summary>Returns the hash code for this instance.</summary>
	/// <returns>
	/// A 32-bit signed integer that is the hash code for this instance.
	/// </returns>
	/// <filterpriority>2</filterpriority>
	public override int GetHashCode() {
		var num1 = CommitPosition;
		var num2 = num1.GetHashCode() * 397;
		num1 = PreparePosition;
		var hashCode = num1.GetHashCode();
		return num2 ^ hashCode;
	}

	/// <summary>
	/// Returns the fully qualified type name of this instance.
	/// </summary>
	/// <returns>
	/// A <see cref="T:System.String" /> containing a fully qualified type name.
	/// </returns>
	/// <filterpriority>2</filterpriority>
	public override string ToString() {
		return $"{CommitPosition}/{PreparePosition}";
	}
}
