using CourseRegistration.Domain;

namespace CourseRegistration.Tests.Domain;

// ReSharper disable once InconsistentNaming
public sealed class when_timing_meetings {
	/// <summary>A meeting that ends before it starts overlaps nothing, so it would slip past every time-conflict check.</summary>
	[Theory]
	[InlineData(600, 500)]
	[InlineData(600, 600)]
	[InlineData(-10, 50)]
	[InlineData(1400, 1500)]
	public void times_that_run_backwards_or_outside_the_day_are_refused(int start, int end) =>
		Assert.Throws<ArgumentOutOfRangeException>(() => new Meeting(DayOfWeek.Monday, start, end));

	[Fact]
	public void times_parse_to_minutes_from_midnight() {
		var meeting = Meeting.At(DayOfWeek.Friday, "09:05", "10:20");
		Assert.Equal((545, 620), (meeting.StartMinute, meeting.EndMinute));
	}
}
