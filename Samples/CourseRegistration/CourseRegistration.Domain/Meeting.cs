namespace CourseRegistration.Domain;

/// <summary>One weekly class meeting, in minutes from midnight.</summary>
public sealed record Meeting {
	private const int MinutesPerDay = 24 * 60;

	/// <summary>The weekday the meeting falls on.</summary>
	public DayOfWeek Day { get; init; }

	/// <summary>When the meeting starts, in minutes from midnight.</summary>
	public int StartMinute { get; init; }

	/// <summary>When the meeting ends, in minutes from midnight.</summary>
	public int EndMinute { get; init; }

	/// <summary>A meeting from <paramref name="startMinute"/> to <paramref name="endMinute"/>, which must end after it starts and within the day.</summary>
	/// <exception cref="ArgumentOutOfRangeException">The times run backwards or outside the day. A meeting that ends before it starts would overlap nothing, slipping past every time-conflict check.</exception>
	public Meeting(DayOfWeek day, int startMinute, int endMinute) {
		ArgumentOutOfRangeException.ThrowIfNegative(startMinute);
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(endMinute, startMinute);
		ArgumentOutOfRangeException.ThrowIfGreaterThan(endMinute, MinutesPerDay);
		Day = day;
		StartMinute = startMinute;
		EndMinute = endMinute;
	}

	/// <summary>True when the two meetings share any minute on the same day.</summary>
	public bool Overlaps(Meeting other) =>
		Day == other.Day && StartMinute < other.EndMinute && other.StartMinute < EndMinute;

	/// <summary>Builds a meeting from <c>HH:mm</c> times, e.g. <c>Meeting.At(DayOfWeek.Monday, "09:00", "09:50")</c>.</summary>
	public static Meeting At(DayOfWeek day, string start, string end) =>
		new(day, ToMinute(start), ToMinute(end));

	private static int ToMinute(string time) {
		var parsed = TimeOnly.ParseExact(time, "HH:mm");
		return parsed.Hour * 60 + parsed.Minute;
	}
}

/// <summary>Meeting lists compared by value, in any order.</summary>
public static class Meetings {
	/// <summary>True when both lists hold the same meetings.</summary>
	public static bool SameAs(this IReadOnlyCollection<Meeting> first, IReadOnlyCollection<Meeting> second) =>
		first.Count == second.Count && first.All(second.Contains);

	/// <summary>True when any meeting in one list overlaps any in the other.</summary>
	public static bool ConflictsWith(this IEnumerable<Meeting> first, IReadOnlyCollection<Meeting> second) =>
		first.Any(m => second.Any(m.Overlaps));
}
