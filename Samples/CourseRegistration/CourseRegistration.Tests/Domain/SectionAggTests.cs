using CourseRegistration.Domain;
using static CourseRegistration.Tests.Aggregates;

namespace CourseRegistration.Tests.Domain;

public sealed class SectionAggTests {
	[Fact]
	public void offering_a_section_raises_one_event_carrying_everything_offered() {
		var id = SectionAgg.IdFor("2026FA", "CHEM 101", 2);
		var section = new SectionAgg(" 2026FA ", "CHEM 101", 2, "General Chemistry", 4, 30, Times.MonWed9, Source());

		Assert.Collection(section.TakeEvents(), e => {
			var offered = Assert.IsType<SectionOffered>(e);
			Assert.Equal(id, offered.SectionId);
			Assert.Equal("2026FA", offered.Term);
			Assert.Equal(2, offered.SectionNumber);
			Assert.Equal(30, offered.Capacity);
			Assert.Equal(Times.MonWed9, offered.Meetings);
		});
		Assert.Equal(id, section.Id);
	}

	public static TheoryData<string, string, int, int, int, Type> BadOffers => new() {
		{ " ", "CHEM 101", 1, 4, 30, typeof(ArgumentException) },
		{ "2026FA", "", 1, 4, 30, typeof(ArgumentException) },
		{ "2026FA", "CHEM 101", 0, 4, 30, typeof(ArgumentOutOfRangeException) },
		{ "2026FA", "CHEM 101", 1, 0, 30, typeof(ArgumentOutOfRangeException) },
		{ "2026FA", "CHEM 101", 1, 4, -1, typeof(ArgumentOutOfRangeException) }
	};

	[Theory]
	[MemberData(nameof(BadOffers))]
	public void an_invalid_offer_is_refused(string term, string course, int number, int credits, int capacity, Type expected) =>
		Assert.Throws(expected, () => new SectionAgg(term, course, number, "Title", credits, capacity, Times.MonWed9, Source()));

	[Fact]
	public void a_section_id_is_its_catalog_name_ignoring_case_and_space() {
		Assert.Equal(SectionAgg.IdFor("2026FA", "CHEM 101", 1), SectionAgg.IdFor(" 2026fa", "chem 101 ", 1));
		Assert.NotEqual(SectionAgg.IdFor("2026FA", "CHEM 101", 1), SectionAgg.IdFor("2026FA", "CHEM 101", 2));
		Assert.NotEqual(SectionAgg.IdFor("2026FA", "CHEM 101", 1), SectionAgg.IdFor("2027SP", "CHEM 101", 1));
	}

	[Fact]
	public void a_reservation_carries_what_the_schedule_needs_to_check_it() {
		var section = Section(credits: 3, meetings: Times.TueThu13);
		section.Take();
		var schedule = Guid.NewGuid();

		section.Reserve(schedule);

		var reserved = Assert.IsType<SeatReserved>(Assert.Single(section.TakeEvents()));
		Assert.Equal(schedule, reserved.ScheduleId);
		Assert.Equal(("2026FA", "CHEM 101", 3), (reserved.Term, reserved.CourseCode, reserved.Credits));
		Assert.Equal(Times.TueThu13, reserved.Meetings);
		Assert.Null(reserved.ReplacesSectionId);
		Assert.Equal(1, section.SeatsHeld);
	}

	[Fact]
	public void a_full_section_refuses_the_next_reservation() {
		var section = Section(capacity: 1);
		section.Reserve(Guid.NewGuid());
		section.Take();
		var late = Guid.NewGuid();

		section.Reserve(late);

		var refused = Assert.IsType<SeatRefused>(Assert.Single(section.TakeEvents()));
		Assert.Equal(late, refused.ScheduleId);
		Assert.Equal(1, section.SeatsHeld);
	}

	[Fact]
	public void reserving_twice_for_one_schedule_holds_one_seat() {
		var section = Section();
		var schedule = Guid.NewGuid();
		section.Reserve(schedule);
		section.Take();

		section.Reserve(schedule);

		Assert.Empty(section.TakeEvents());
		Assert.Equal(1, section.SeatsHeld);
	}

	[Fact]
	public void releasing_a_seat_frees_it_and_a_second_release_does_nothing() {
		var section = Section(capacity: 1);
		var schedule = Guid.NewGuid();
		section.Reserve(schedule);
		section.Take();

		section.Release(schedule);
		section.Release(schedule);

		Assert.IsType<SeatReleased>(Assert.Single(section.TakeEvents()));
		Assert.Equal(0, section.SeatsHeld);
	}

	[Fact]
	public void lowering_capacity_below_the_seats_held_keeps_them_and_refuses_new_ones() {
		var section = Section(capacity: 2);
		section.Reserve(Guid.NewGuid());
		section.Reserve(Guid.NewGuid());
		section.ChangeCapacity(1);
		section.Take();

		section.Reserve(Guid.NewGuid());

		Assert.IsType<SeatRefused>(Assert.Single(section.TakeEvents()));
		Assert.Equal(2, section.SeatsHeld);
	}

	[Fact]
	public void unchanged_meetings_and_capacity_raise_nothing() {
		var section = Section(capacity: 10, meetings: Times.MonWed9);
		section.Take();

		section.Reschedule([Times.MonWed9[1], Times.MonWed9[0]]);
		section.ChangeCapacity(10);
		section.Cancel();
		section.Take();
		section.Cancel();

		Assert.Empty(section.TakeEvents());
	}

	[Fact]
	public void cancelling_names_every_holder_and_blocks_further_changes() {
		var section = Section();
		var first = Guid.NewGuid();
		var second = Guid.NewGuid();
		section.Reserve(first);
		section.Reserve(second);
		section.Take();

		section.Cancel();

		var cancelled = Assert.IsType<SectionCancelled>(Assert.Single(section.Take()));
		Assert.Equal(new[] { first, second }.Order(), cancelled.HeldBy.Order());
		Assert.Equal(0, section.SeatsHeld);
		Assert.Throws<InvalidOperationException>(() => section.Reserve(Guid.NewGuid()));
		Assert.Throws<InvalidOperationException>(() => section.Reschedule(Times.Fri15));
		Assert.Throws<InvalidOperationException>(() => section.ChangeCapacity(5));
	}

	[Fact]
	public void a_section_restored_from_its_events_holds_the_same_seats() {
		var section = Section(capacity: 2);
		var kept = Guid.NewGuid();
		var released = Guid.NewGuid();
		section.Reserve(kept);
		section.Reserve(released);
		section.Release(released);
		section.ChangeCapacity(3);

		var restored = Rehydrate<SectionAgg>(section.TakeEvents());
		restored.Source = Source();

		Assert.Equal(section.Id, restored.Id);
		Assert.Equal((1, 3), (restored.SeatsHeld, restored.Capacity));
		restored.Reserve(kept);
		Assert.Empty(restored.TakeEvents());
		restored.Source = Source();
		restored.Reserve(released);
		Assert.IsType<SeatReserved>(Assert.Single(restored.TakeEvents()));
	}
}
