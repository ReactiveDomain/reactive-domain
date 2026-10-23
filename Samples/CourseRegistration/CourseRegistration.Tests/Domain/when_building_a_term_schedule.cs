using CourseRegistration.Domain;
using static CourseRegistration.Tests.Aggregates;

namespace CourseRegistration.Tests.Domain;

// ReSharper disable once InconsistentNaming
public sealed class when_building_a_term_schedule {
	private static readonly Guid Chem = Guid.NewGuid();
	private static readonly Guid Bio = Guid.NewGuid();
	private static readonly Guid Hist = Guid.NewGuid();

	private static TermScheduleAgg WithChem(int maxCredits = 18) {
		var schedule = Schedule(maxCredits);
		schedule.Add(Guid.NewGuid(), Chem, "2026FA", "CHEM 101", 4, Times.MonWed9);
		schedule.Take();
		return schedule;
	}

	[Fact]
	public void opening_a_schedule_raises_one_event() {
		var id = Guid.NewGuid();
		var student = Guid.NewGuid();
		var schedule = new TermScheduleAgg(id, student, "2026FA", 16, Source());

		var opened = Assert.IsType<TermScheduleOpened>(Assert.Single(schedule.TakeEvents()));
		Assert.Equal((id, student, "2026FA", 16), (opened.ScheduleId, opened.StudentId, opened.Term, opened.MaxCredits));
		Assert.Equal((id, student), (schedule.Id, schedule.StudentId));
	}

	public static TheoryData<Guid, Guid, string, int, Type> BadOpens => new() {
		{ Guid.Empty, Guid.NewGuid(), "2026FA", 18, typeof(ArgumentException) },
		{ Guid.NewGuid(), Guid.Empty, "2026FA", 18, typeof(ArgumentException) },
		{ Guid.NewGuid(), Guid.NewGuid(), "  ", 18, typeof(ArgumentException) },
		{ Guid.NewGuid(), Guid.NewGuid(), "2026FA", 0, typeof(ArgumentOutOfRangeException) }
	};

	[Theory]
	[MemberData(nameof(BadOpens))]
	public void an_invalid_schedule_is_refused(Guid id, Guid student, string term, int maxCredits, Type expected) =>
		Assert.Throws(expected, () => new TermScheduleAgg(id, student, term, maxCredits, Source()));

	[Fact]
	public void a_reserved_course_that_fits_is_added() {
		var schedule = WithChem();

		schedule.Add(Guid.NewGuid(), Bio, "2026FA", "BIO 110", 4, Times.TueThu13);

		var added = Assert.IsType<CourseAdded>(Assert.Single(schedule.TakeEvents()));
		Assert.Equal(Bio, added.SectionId);
		Assert.Equal(8, schedule.Credits);
		Assert.Equal(new[] { Chem, Bio }.Order(), schedule.Sections.Order());
	}

	public static TheoryData<string, string, int, Meeting[], AddRejection> Rejections => new() {
		{ "2027SP", "BIO 110", 4, Times.TueThu13, AddRejection.WrongTerm },
		{ "2026FA", "chem 101", 4, Times.TueThu13, AddRejection.AlreadyTakingCourse },
		{ "2026FA", "BIO 110", 5, Times.TueThu13, AddRejection.CreditLimit },
		{ "2026FA", "BIO 110", 4, Times.MonWed930, AddRejection.TimeConflict }
	};

	[Theory]
	[MemberData(nameof(Rejections))]
	public void a_course_that_breaks_a_rule_is_rejected_not_thrown(string term, string course, int credits, Meeting[] meetings, AddRejection reason) {
		var schedule = WithChem(maxCredits: 8);

		schedule.Add(Guid.NewGuid(), Bio, term, course, credits, meetings);

		var rejected = Assert.IsType<CourseAddRejected>(Assert.Single(schedule.TakeEvents()));
		Assert.Equal((Bio, reason), (rejected.SectionId, rejected.Reason));
		Assert.Equal([Chem], schedule.Sections);
	}

	[Fact]
	public void adding_a_section_already_on_the_schedule_raises_nothing() {
		var schedule = WithChem();

		schedule.Add(Guid.NewGuid(), Chem, "2026FA", "CHEM 101", 4, Times.MonWed9);

		Assert.Empty(schedule.TakeEvents());
	}

	[Fact]
	public void a_swap_judges_the_new_course_as_if_the_old_one_were_gone() {
		// Same course, same time, more credits than the limit allows alongside the old one: only a swap fits.
		var schedule = WithChem(maxCredits: 5);
		var reservation = Guid.NewGuid();

		schedule.Swap(reservation, Chem, Hist, "2026FA", "CHEM 101", 5, Times.MonWed9);

		var swapped = Assert.IsType<CourseSwapped>(Assert.Single(schedule.Take()));
		Assert.Equal((Chem, Hist), (swapped.DroppedSectionId, swapped.AddedSectionId));
		Assert.Equal([Hist], schedule.Sections);
		schedule.Swap(reservation, Chem, Hist, "2026FA", "CHEM 101", 5, Times.MonWed9);
		Assert.Empty(schedule.TakeEvents());
	}

	[Fact]
	public void a_rejected_swap_keeps_the_old_course() {
		var schedule = WithChem(maxCredits: 5);

		schedule.Swap(Guid.NewGuid(), Chem, Hist, "2026FA", "HIST 200", 6, Times.Fri15);

		var rejected = Assert.IsType<CourseAddRejected>(Assert.Single(schedule.TakeEvents()));
		Assert.Equal((Hist, AddRejection.CreditLimit), (rejected.SectionId, rejected.Reason));
		Assert.Equal([Chem], schedule.Sections);
	}

	[Fact]
	public void swapping_out_a_section_the_student_does_not_have_is_rejected() {
		var schedule = WithChem();

		schedule.Swap(Guid.NewGuid(), Bio, Hist, "2026FA", "HIST 200", 3, Times.Fri15);

		var rejected = Assert.IsType<CourseAddRejected>(Assert.Single(schedule.TakeEvents()));
		Assert.Equal(AddRejection.NotEnrolledInDroppedSection, rejected.Reason);
	}

	[Fact]
	public void dropping_is_idempotent() {
		var schedule = WithChem();

		schedule.Drop(Chem);
		schedule.Drop(Chem);
		schedule.Drop(Bio);

		Assert.IsType<CourseDropped>(Assert.Single(schedule.TakeEvents()));
		Assert.Empty(schedule.Sections);
	}

	[Fact]
	public void after_add_drop_closes_adds_are_rejected_drops_throw_and_cancellations_still_land() {
		var schedule = WithChem();
		schedule.Close();
		schedule.Close();
		Assert.IsType<AddDropClosed>(Assert.Single(schedule.Take()));

		schedule.Add(Guid.NewGuid(), Bio, "2026FA", "BIO 110", 4, Times.TueThu13);
		Assert.Equal(AddRejection.AddDropClosed, Assert.IsType<CourseAddRejected>(Assert.Single(schedule.Take())).Reason);
		schedule.Swap(Guid.NewGuid(), Chem, Bio, "2026FA", "BIO 110", 4, Times.TueThu13);
		Assert.Equal(AddRejection.AddDropClosed, Assert.IsType<CourseAddRejected>(Assert.Single(schedule.Take())).Reason);
		Assert.Throws<InvalidOperationException>(() => schedule.Drop(Chem));

		schedule.RemoveCancelled(Chem);
		Assert.IsType<CourseCancelled>(Assert.Single(schedule.TakeEvents()));
		Assert.Empty(schedule.Sections);
	}

	[Fact]
	public void a_schedule_restored_from_its_events_enforces_the_same_rules() {
		var schedule = Schedule(maxCredits: 8);
		schedule.Add(Guid.NewGuid(), Chem, "2026FA", "CHEM 101", 4, Times.MonWed9);
		schedule.Add(Guid.NewGuid(), Bio, "2026FA", "BIO 110", 4, Times.TueThu13);
		schedule.Swap(Guid.NewGuid(), Bio, Hist, "2026FA", "HIST 200", 3, Times.Fri15);
		schedule.Add(Guid.NewGuid(), Bio, "2026FA", "BIO 110", 4, Times.TueThu13); // rejected: 11 credits
		schedule.Close();

		var restored = Rehydrate<TermScheduleAgg>(schedule.TakeEvents());
		restored.Source = Source();

		Assert.Equal((schedule.Id, schedule.StudentId, "2026FA"), (restored.Id, restored.StudentId, restored.Term));
		Assert.Equal(new[] { Chem, Hist }.Order(), restored.Sections.Order());
		Assert.Equal(7, restored.Credits);
		Assert.True(restored.IsClosed);
		restored.Add(Guid.NewGuid(), Bio, "2026FA", "BIO 110", 1, Times.TueThu13);
		Assert.Equal(AddRejection.AddDropClosed, Assert.IsType<CourseAddRejected>(Assert.Single(restored.TakeEvents())).Reason);
	}
}
