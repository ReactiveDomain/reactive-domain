using CourseRegistration.Domain;
using CourseRegistration.ReadModels;
using CourseRegistration.ReadModels.Persistence;
using CourseRegistration.Tests.Persistence;

namespace CourseRegistration.Tests.ReadModels;

/// <summary>
/// The read models end to end, against each database: events through RD's in-memory store, rows and
/// checkpoints into the database, and restarts that resume from what was saved.
/// </summary>
// ReSharper disable once InconsistentNaming
public abstract class when_projecting_registrations : IDisposable {
	private readonly Func<IProjectionDb> _newDb;
	private readonly Harness _h;

	protected when_projecting_registrations(Func<IProjectionDb> newDb) {
		_newDb = newDb;
		_h = new Harness(newDb());
	}

	public void Dispose() => _h.Dispose();

	private Guid Enrol(Guid schedule, Guid section) {
		_h.Send(new RequestCourse(schedule, section));
		_h.Settle();
		return section;
	}

	private long? Saved(string projection, Type aggregate) =>
		_h.Db.Checkpoints.Load(projection).SingleOrDefault(c => c.StreamName == _h.Connection.StreamNamer.GenerateForCategory(aggregate))?.Version;

	private long LastEventIn(Type aggregate) =>
		_h.Store.ReadStreamForward(_h.Connection.StreamNamer.GenerateForCategory(aggregate), 0, 4096).Events.Length - 1;

	// ── Section catalog: one category, one counter ──────────────────────────

	[Fact]
	public void the_catalog_projects_sections_and_counts_held_seats() {
		var catalog = _h.StartCatalog();
		var chem = _h.Offer(capacity: 2, meetings: Times.TueThu13);
		Enrol(_h.Open(), chem);
		var dropper = _h.Open();
		Enrol(dropper, chem);
		_h.Send(new DropCourse(dropper, chem));
		_h.Settle();
		_h.CatchUp(catalog);

		var row = _h.Sections().Get(chem)!;
		Assert.Equal(("CHEM 101", 2, 1, 1), (row.CourseCode, row.Capacity, row.SeatsHeld, row.SeatsOpen));
		Assert.Equal(Times.TueThu13, row.Meetings);
		Assert.Equal(LastEventIn(typeof(SectionAgg)), Saved(catalog.Name, typeof(SectionAgg)));
	}

	[Fact]
	public void history_already_written_is_caught_up_and_saved_in_one_transaction() {
		var chem = _h.Offer();
		for (var i = 0; i < 5; i++)
			Enrol(_h.Open(), chem);
		var counting = new FaultyDb(_h.Db);

		var catalog = _h.StartCatalog(counting);

		Assert.Equal(1, counting.Commits);
		Assert.Equal(5, _h.Sections().Get(chem)!.SeatsHeld);
		Assert.Equal(LastEventIn(typeof(SectionAgg)), Saved(catalog.Name, typeof(SectionAgg)));
	}

	/// <summary>
	/// A restart reads only what follows the saved checkpoint. Rereading from the start would add
	/// every reservation to a count that already holds it.
	/// </summary>
	[Fact]
	public void a_restarted_catalog_resumes_after_its_checkpoint_without_counting_twice() {
		var chem = _h.Offer();
		var first = _h.StartCatalog();
		Enrol(_h.Open(), chem);
		Enrol(_h.Open(), chem);
		_h.CatchUp(first);
		first.Dispose();
		Enrol(_h.Open(), chem);

		var second = _h.StartCatalog();
		_h.CatchUp(second);

		Assert.Equal(3, _h.Sections().Get(chem)!.SeatsHeld);
		Assert.Equal(LastEventIn(typeof(SectionAgg)), Saved(second.Name, typeof(SectionAgg)));
	}

	/// <summary>The rows and the checkpoint commit together or not at all; the buffered change is written by the next flush.</summary>
	[Fact]
	public void a_write_that_throws_saves_neither_rows_nor_checkpoint_and_the_next_write_saves_both() {
		var faulty = new FaultyDb(_h.Db);
		var chem = _h.Offer();
		var catalog = _h.StartCatalog(faulty);
		var before = Saved(catalog.Name, typeof(SectionAgg));

		faulty.ThrowBeforeNextCommit();
		Enrol(_h.Open(), chem);
		Harness.Eventually(() => catalog.Idle && catalog.Version >= 2, "the reservation was not handled");
		Assert.Equal(0, _h.Sections().Get(chem)!.SeatsHeld);
		Assert.Equal(before, Saved(catalog.Name, typeof(SectionAgg)));

		Enrol(_h.Open(), chem);
		_h.CatchUp(catalog);

		Harness.Eventually(() => _h.Sections().Get(chem)!.SeatsHeld == 2, "the buffered reservation was not written");
		Assert.Equal(LastEventIn(typeof(SectionAgg)), Saved(catalog.Name, typeof(SectionAgg)));
	}

	[Fact]
	public void a_restart_after_a_write_that_threw_replays_the_event_it_lost() {
		var faulty = new FaultyDb(_h.Db);
		var chem = _h.Offer();
		var first = _h.StartCatalog(faulty);
		faulty.ThrowBeforeNextCommit();
		Enrol(_h.Open(), chem);
		Harness.Eventually(() => first.Idle && first.Version >= 2, "the reservation was not handled");
		first.Dispose();

		var second = _h.StartCatalog();
		_h.CatchUp(second);

		Assert.Equal(1, _h.Sections().Get(chem)!.SeatsHeld);
	}

	/// <summary>
	/// The queue would log the throw and apply the next event, whose flush would checkpoint past the
	/// one never applied. The projection stops instead, and its checkpoint stays put.
	/// </summary>
	[Fact]
	public void a_handler_that_throws_while_live_stops_the_projection_at_its_last_written_event() {
		var exploding = _h.Start(new ExplodingCatalogRM(_h.Connection, _h.Db, explodeOn: "boom"));
		var first = _h.Offer("CHEM 101", title: "General Chemistry");
		_h.CatchUp(exploding);
		var checkpoint = Saved(exploding.Name, typeof(SectionAgg));

		_h.Offer("BIO 110", title: "boom");
		_h.Offer("HIST 200", title: "Survey");
		_h.CatchUp(exploding);

		Assert.IsType<InvalidOperationException>(exploding.Fault);
		Assert.Equal([first], _h.Db.Rows<Guid, SectionRow>().All().Select(r => r.Id));
		Assert.Equal(checkpoint, Saved(exploding.Name, typeof(SectionAgg)));
	}

	[Fact]
	public void a_handler_that_throws_while_catching_up_writes_nothing_and_a_restart_replays_it_all() {
		_h.Offer("CHEM 101", title: "General Chemistry");
		_h.Offer("BIO 110", title: "boom");
		_h.Offer("HIST 200", title: "Survey");

		var exploding = _h.Start(new ExplodingCatalogRM(_h.Connection, _h.Db, explodeOn: "boom"));

		Assert.NotNull(exploding.Fault);
		Assert.Empty(_h.Db.Rows<Guid, SectionRow>().All());
		Assert.Null(Saved(exploding.Name, typeof(SectionAgg)));
		exploding.Dispose();
		var fixedModel = _h.Start(new ExplodingCatalogRM(_h.Connection, _h.Db, explodeOn: null));
		Assert.Null(fixedModel.Fault);
		Assert.Equal(3, _h.Db.Rows<Guid, SectionRow>().All().Count);
	}

	[Fact]
	public void the_schedules_with_a_section_are_found_through_their_array_column() {
		var timetable = _h.StartTimetable();
		var chem = _h.Offer();
		var bio = _h.Offer("BIO 110", meetings: Times.TueThu13);
		var first = _h.Open();
		var second = _h.Open();
		Enrol(first, chem);
		Enrol(second, chem);
		Enrol(second, bio);
		_h.Send(new DropCourse(first, chem));
		_h.Settle();
		_h.CatchUp(timetable);

		var queries = _h.Timetables();
		Assert.Equal([second], queries.SchedulesWith(chem).Select(s => s.ScheduleId));
		Assert.Equal(new[] { chem, bio }.Order(), queries.Schedule(second)!.SectionIds);
		Assert.Empty(queries.Schedule(first)!.SectionIds);
	}

	// ── Timetable: two categories, one checkpoint each, fan-out ─────────────

	[Fact]
	public void the_timetable_lists_each_course_with_its_section_and_the_schedule_total() {
		var timetable = _h.StartTimetable();
		var chem = _h.Offer(title: "General Chemistry", meetings: Times.MonWed9);
		var bio = _h.Offer("BIO 110", title: "Cells", credits: 3, meetings: Times.TueThu13);
		var student = Guid.NewGuid();
		var schedule = _h.Open(studentId: student);
		Enrol(schedule, chem);
		Enrol(schedule, bio);
		_h.CatchUp(timetable);

		var queries = _h.Timetables();
		Assert.Equal(["BIO 110", "CHEM 101"], queries.Timetable(schedule).Select(e => e.CourseCode));
		var bioEntry = queries.Timetable(schedule).First();
		Assert.Equal("Cells", bioEntry.Title);
		Assert.Equal(Times.TueThu13, bioEntry.Meetings);
		Assert.Equal((student, 7), (queries.Schedule(schedule)!.StudentId, queries.Schedule(schedule)!.Credits));
		Assert.Equal([student], queries.Roster(chem));
	}

	[Fact]
	public void rescheduling_a_section_rewrites_every_entry_for_it_in_one_write() {
		var chem = _h.Offer(meetings: Times.MonWed9);
		var schedules = Enumerable.Range(0, 4).Select(_ => _h.Open()).ToList();
		schedules.ForEach(s => Enrol(s, chem));
		var counting = new FaultyDb(_h.Db);
		var timetable = _h.StartTimetable(counting);
		var commits = counting.Commits;

		_h.Send(new RescheduleSection(chem, Times.Fri15));
		_h.CatchUp(timetable);

		Harness.Eventually(() => counting.Commits == commits + 1, "the reschedule was not written");
		Assert.All(schedules, s => Assert.Equal(Times.Fri15, Assert.Single(_h.Timetables().Timetable(s)).Meetings));
	}

	[Fact]
	public void a_swap_a_drop_and_a_cancellation_each_take_the_entry_off() {
		var timetable = _h.StartTimetable();
		var early = _h.Offer(number: 1, meetings: Times.MonWed9);
		var late = _h.Offer(number: 2, meetings: Times.TueThu13);
		var bio = _h.Offer("BIO 110", meetings: Times.Fri15);
		var hist = _h.Offer("HIST 200", meetings: Times.MonWed930.Select(m => m with { Day = DayOfWeek.Saturday }).ToArray());
		var schedule = _h.Open();
		Enrol(schedule, early);
		Enrol(schedule, bio);
		Enrol(schedule, hist);

		_h.Send(new RequestSwap(schedule, early, late));
		_h.Send(new DropCourse(schedule, bio));
		_h.Send(new CancelSection(hist));
		_h.Settle();
		_h.CatchUp(timetable);

		var entry = Assert.Single(_h.Timetables().Timetable(schedule));
		Assert.Equal((late, 2), (entry.SectionId, entry.SectionNumber));
		Assert.Equal(4, _h.Timetables().Schedule(schedule)!.Credits);
	}

	[Fact]
	public void the_timetable_saves_a_checkpoint_for_each_category_it_reads() {
		var timetable = _h.StartTimetable();
		Enrol(_h.Open(), _h.Offer());
		_h.CatchUp(timetable);

		Assert.Equal(LastEventIn(typeof(SectionAgg)), Saved(timetable.Name, typeof(SectionAgg)));
		Assert.Equal(LastEventIn(typeof(TermScheduleAgg)), Saved(timetable.Name, typeof(TermScheduleAgg)));
	}

	/// <summary>
	/// Catching up reads the two categories side by side, so a course can arrive before its section is
	/// described, and the original meetings after the rescheduled ones were applied to the course.
	/// Either way the end state is the same.
	/// </summary>
	[Fact]
	public void caught_up_from_scratch_the_timetable_matches_one_built_live() {
		var live = _h.StartTimetable();
		var chem = _h.Offer(meetings: Times.MonWed9);
		var bio = _h.Offer("BIO 110", meetings: Times.TueThu13);
		var schedules = Enumerable.Range(0, 3).Select(_ => _h.Open()).ToList();
		schedules.ForEach(s => Enrol(s, chem));
		Enrol(schedules[0], bio);
		_h.Send(new RescheduleSection(chem, Times.Fri15));
		_h.Send(new CloseAddDrop(schedules[1]));
		_h.Settle();
		_h.CatchUp(live);

		var fresh = _newDb();
		var rebuilt = _h.StartTimetable(fresh);
		_h.CatchUp(rebuilt);

		foreach (var schedule in schedules) {
			Assert.Equivalent(_h.Timetables().Schedule(schedule), _h.Timetables(fresh).Schedule(schedule));
			Assert.Equivalent(_h.Timetables().Timetable(schedule), _h.Timetables(fresh).Timetable(schedule));
		}
		Assert.True(_h.Timetables(fresh).Schedule(schedules[1])!.IsClosed);
		Assert.Equal(Times.Fri15, _h.Timetables(fresh).Timetable(schedules[2]).Single().Meetings);
	}

	[Fact]
	public void a_restarted_timetable_resumes_each_category_from_its_own_checkpoint() {
		var chem = _h.Offer();
		var schedule = _h.Open();
		var first = _h.StartTimetable();
		Enrol(schedule, chem);
		_h.CatchUp(first);
		first.Dispose();
		var bio = _h.Offer("BIO 110", meetings: Times.TueThu13);
		Enrol(schedule, bio);

		var second = _h.StartTimetable();
		_h.CatchUp(second);

		Assert.Equal(["BIO 110", "CHEM 101"], _h.Timetables().Timetable(schedule).Select(e => e.CourseCode));
		Assert.Equal(8, _h.Timetables().Schedule(schedule)!.Credits);
	}
}

// ReSharper disable once InconsistentNaming
public sealed class when_projecting_registrations_in_memory() : when_projecting_registrations(() => new InMemoryProjectionDb());

[Collection(PostgresCollection.Name)]
// ReSharper disable once InconsistentNaming
public sealed class when_projecting_registrations_in_postgres(PostgresFixture postgres) : when_projecting_registrations(() => new PostgresProjectionDb(postgres.NewDatabase()));

/// <summary>A catalog whose handler throws on one title, standing in for a projection bug.</summary>
[OwnedTables(typeof(SectionRow))]
internal sealed class ExplodingCatalogRM : ProjectionRM, ReactiveDomain.Messaging.Bus.IHandle<SectionOffered> {
	private readonly string? _explodeOn;
	private readonly ReactiveDomain.Foundation.WriteBuffer<Guid, SectionRow> _sections;

	public ExplodingCatalogRM(ReactiveDomain.Foundation.IConfiguredConnection connection, IProjectionDb db, string? explodeOn)
		: base(nameof(ExplodingCatalogRM), connection, db) {
		_explodeOn = explodeOn;
		_sections = OwnTable(db.Rows<Guid, SectionRow>());
		EventStream.Subscribe<SectionOffered>(this);
	}

	protected override void Load() { }

	protected override IReadOnlyList<ReactiveDomain.Foundation.StreamStarter> Streams() => [Category<SectionAgg>()];

	void ReactiveDomain.Messaging.Bus.IHandle<SectionOffered>.Handle(SectionOffered e) {
		_sections.Upsert(e.SectionId, new SectionRow { Id = e.SectionId, Term = e.Term, CourseCode = e.CourseCode, SectionNumber = e.SectionNumber, Title = e.Title });
		if (e.Title == _explodeOn)
			throw new InvalidOperationException($"cannot project {e.Title}");
	}
}
