using CourseRegistration.Domain;
using CourseRegistration.ReadModels;
using CourseRegistration.ReadModels.Persistence;
using Dapper;
using Npgsql;

namespace CourseRegistration.Tests.Persistence;

/// <summary>Upgrading a database in place, and rebuilding projections with a reset key.</summary>
[Collection(PostgresCollection.Name)]
// ReSharper disable once InconsistentNaming
public sealed class when_evolving_the_schema_in_postgres(PostgresFixture postgres) {
	/// <summary>The tables as the first release shipped them, before the v2 columns.</summary>
	private const string FirstRelease = """
		CREATE TABLE projection_checkpoints (
			projection TEXT NOT NULL, stream_name TEXT NOT NULL, version BIGINT NOT NULL, saved_at TIMESTAMPTZ NOT NULL,
			PRIMARY KEY (projection, stream_name));
		CREATE TABLE sections (
			id UUID PRIMARY KEY, term TEXT NOT NULL, course_code TEXT NOT NULL, section_number INTEGER NOT NULL,
			title TEXT NOT NULL, credits INTEGER NOT NULL, capacity INTEGER NOT NULL, seats_held INTEGER NOT NULL,
			is_cancelled BOOLEAN NOT NULL, meetings JSONB NOT NULL);
		CREATE TABLE schedules (
			schedule_id UUID PRIMARY KEY, student_id UUID NOT NULL, term TEXT NOT NULL, credits INTEGER NOT NULL,
			is_closed BOOLEAN NOT NULL);
		CREATE TABLE timetable_entries (
			id UUID PRIMARY KEY, schedule_id UUID NOT NULL, section_id UUID NOT NULL, student_id UUID NOT NULL,
			term TEXT NOT NULL, course_code TEXT NOT NULL, section_number INTEGER NOT NULL, title TEXT NOT NULL,
			credits INTEGER NOT NULL, meetings JSONB NOT NULL, UNIQUE (schedule_id, section_id));
		""";

	[Fact]
	public void a_first_release_database_upgrades_in_place_and_keeps_its_rows() {
		var data = postgres.NewEmptyDatabase();
		var section = Guid.NewGuid();
		var schedule = Guid.NewGuid();
		using (var connection = data.OpenConnection()) {
			connection.Execute(FirstRelease);
			connection.Execute("""
				INSERT INTO sections VALUES (@section, '2026FA', 'Chem 101', 1, 'General Chemistry', 4, 30, 2, FALSE, @meetings::jsonb);
				INSERT INTO schedules VALUES (@schedule, @student, '2026FA', 4, FALSE);
				INSERT INTO timetable_entries VALUES (@entry, @schedule, @section, @student, '2026FA', 'Chem 101', 1, 'General Chemistry', 4, @meetings::jsonb);
				""", new {
				section, schedule, student = Guid.NewGuid(), entry = Guid.NewGuid(),
				meetings = MeetingJson.Write(Times.MonWed9)
			});
		}

		ProjectionSchema.Create(data);
		ProjectionSchema.Create(data);

		var db = new PostgresProjectionDb(data);
		var upgraded = db.Rows<Guid, SectionRow>().Get(section)!;
		Assert.Equal(("CHEM", 2), (upgraded.Department, upgraded.SeatsHeld));
		Assert.Equal([(int)DayOfWeek.Monday, (int)DayOfWeek.Wednesday], upgraded.MeetingDays);
		Assert.Equal([section], db.Rows<Guid, ScheduleRow>().Get(schedule)!.SectionIds);
	}

	[Fact]
	public void a_reset_key_rebuilds_the_projections_once_and_leaves_what_no_replay_could_rebuild() {
		var data = postgres.NewDatabase();
		using var h = new Harness(new PostgresProjectionDb(data));
		var catalog = h.StartCatalog();
		var timetable = h.StartTimetable();
		var chem = h.Offer();
		var schedule = h.Open();
		h.Send(new RequestCourse(schedule, chem));
		h.Settle();
		h.CatchUp(catalog);
		h.CatchUp(timetable);
		catalog.Dispose();
		timetable.Dispose();
		new ReminderLedger(data).TryRecord(Guid.NewGuid(), "2026FA", "closing", "a@example.edu");

		var first = ProjectionReset.ResetOnce(data, "deploy-42", typeof(SectionCatalogRM), typeof(StudentTimetableRM));
		var again = ProjectionReset.ResetOnce(data, "deploy-42", typeof(SectionCatalogRM), typeof(StudentTimetableRM));

		Assert.True(first);
		Assert.False(again);
		Assert.Equal(0, Count(data, "sections") + Count(data, "schedules") + Count(data, "timetable_entries") + Count(data, "projection_checkpoints"));
		Assert.Equal(1, Count(data, "reminder_sends"));

		var rebuiltCatalog = h.StartCatalog();
		var rebuiltTimetable = h.StartTimetable();
		h.CatchUp(rebuiltCatalog);
		h.CatchUp(rebuiltTimetable);
		Assert.Equal(1, h.Sections().Get(chem)!.SeatsHeld);
		Assert.Equal([chem], h.Timetables().Schedule(schedule)!.SectionIds);
	}

	[Fact]
	public async Task instances_starting_together_with_a_new_key_reset_once() {
		var data = postgres.NewDatabase();

		var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
			Task.Run(() => ProjectionReset.ResetOnce(data, "deploy-43", typeof(SectionCatalogRM)))));

		Assert.Single(results, r => r);
	}

	[Fact]
	public void rebuilding_one_projection_leaves_the_others_tables_alone() {
		var data = postgres.NewDatabase();
		using var h = new Harness(new PostgresProjectionDb(data));
		var catalog = h.StartCatalog();
		var timetable = h.StartTimetable();
		var schedule = h.Open();
		h.Send(new RequestCourse(schedule, h.Offer()));
		h.Settle();
		h.CatchUp(catalog);
		h.CatchUp(timetable);
		catalog.Dispose();
		timetable.Dispose();

		ProjectionReset.Rebuild(data, typeof(SectionCatalogRM));

		Assert.Equal(0, Count(data, "sections"));
		Assert.Equal(1, Count(data, "schedules"));
		Assert.Empty(new PostgresProjectionDb(data).Checkpoints.Load(nameof(SectionCatalogRM)));
		Assert.NotEmpty(new PostgresProjectionDb(data).Checkpoints.Load(nameof(StudentTimetableRM)));
	}

	private static int Count(NpgsqlDataSource data, string table) {
		using var connection = data.OpenConnection();
		return connection.ExecuteScalar<int>($"SELECT COUNT(*) FROM {table}");
	}
}
