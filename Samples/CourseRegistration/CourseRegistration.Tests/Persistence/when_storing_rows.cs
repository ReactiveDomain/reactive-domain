using CourseRegistration.ReadModels;
using CourseRegistration.ReadModels.Persistence;
using ReactiveDomain;

namespace CourseRegistration.Tests.Persistence;

/// <summary>
/// What every <see cref="IProjectionDb"/> must do, run against each one. A behaviour that differs
/// between memory and Postgres is caught here rather than in production.
/// </summary>
// ReSharper disable once InconsistentNaming
public abstract class when_storing_rows {
	protected abstract IProjectionDb NewDb();

	private static SectionRow Row(string course = "CHEM 101", int number = 1, string term = "2026FA", int capacity = 30, int held = 0) => new() {
		Id = Guid.NewGuid(),
		Term = term,
		CourseCode = course,
		SectionNumber = number,
		Title = "Title",
		Credits = 4,
		Capacity = capacity,
		SeatsHeld = held,
		Meetings = Times.MonWed9
	};

	private static void Write(IProjectionDb db, params SectionRow[] rows) {
		var store = db.Rows<Guid, SectionRow>();
		db.InTransaction(scope => {
			foreach (var row in rows)
				store.Upsert(scope, row);
		});
	}

	[Fact]
	public void an_upsert_replaces_the_row_and_a_read_returns_a_copy() {
		var db = NewDb();
		var store = db.Rows<Guid, SectionRow>();
		var row = Row();
		Write(db, row);
		row.SeatsHeld = 7;
		Assert.Equal(0, store.Get(row.Id)!.SeatsHeld);

		Write(db, row);

		var read = store.Get(row.Id)!;
		Assert.Equal(7, read.SeatsHeld);
		read.SeatsHeld = 99;
		Assert.Equal(7, store.Get(row.Id)!.SeatsHeld);
		Assert.Single(store.All());
	}

	[Fact]
	public void a_fenced_writers_transaction_keeps_neither_rows_nor_checkpoint() {
		var db = NewDb();
		var store = db.Rows<Guid, SectionRow>();
		var old = Guid.NewGuid();
		db.Writers.Claim("catalog", old);
		var row = Row();
		db.InTransaction(scope => {
			db.Writers.Assert(scope, "catalog", old);
			store.Upsert(scope, row);
		});

		db.Writers.Claim("catalog", Guid.NewGuid());

		var late = Row();
		late.Id = row.Id;
		late.SeatsHeld = 9;
		// The assert comes last here: the fence voids the whole transaction, not just what follows it.
		Assert.Throws<FencedWriterException>(() => db.InTransaction(scope => {
			store.Upsert(scope, late);
			db.Checkpoints.Save(scope, "catalog", [new StreamCheckpoint("stream", 5)]);
			db.Writers.Assert(scope, "catalog", old);
		}));
		Assert.Equal(0, store.Get(row.Id)!.SeatsHeld);
		Assert.Empty(db.Checkpoints.Load("catalog"));
	}

	[Fact]
	public void an_insert_never_overwrites_and_says_whether_it_wrote() {
		var db = NewDb();
		var store = db.Rows<Guid, SectionRow>();
		var row = Row();
		var changed = Row();
		changed.Id = row.Id;
		changed.Title = "Changed";
		bool first = false, second = true;

		db.InTransaction(scope => first = store.Insert(scope, row));
		db.InTransaction(scope => second = store.Insert(scope, changed));

		Assert.True(first);
		Assert.False(second);
		Assert.Equal("Title", store.Get(row.Id)!.Title);
	}

	[Fact]
	public void deleting_removes_the_row_and_deleting_again_is_harmless() {
		var db = NewDb();
		var store = db.Rows<Guid, SectionRow>();
		var row = Row();
		Write(db, row);

		db.InTransaction(scope => store.Delete(scope, row.Id));
		db.InTransaction(scope => store.Delete(scope, row.Id));

		Assert.Null(store.Get(row.Id));
	}

	[Fact]
	public void a_jsonb_column_round_trips() {
		var db = NewDb();
		var row = Row();
		row.Meetings = [.. Times.TueThu13, .. Times.Fri15];
		Write(db, row);

		Assert.Equal(row.Meetings, db.Rows<Guid, SectionRow>().Get(row.Id)!.Meetings);
	}

	[Fact]
	public void array_columns_round_trip_and_a_read_never_shares_one() {
		var db = NewDb();
		var sections = db.Rows<Guid, SectionRow>();
		var schedules = db.Rows<Guid, ScheduleRow>();
		var section = Row();
		section.Meetings = [.. Times.TueThu13, .. Times.Fri15];
		var schedule = new ScheduleRow { ScheduleId = Guid.NewGuid(), StudentId = Guid.NewGuid(), Term = "2026FA", SectionIds = [Guid.NewGuid(), Guid.NewGuid()] };
		db.InTransaction(scope => {
			sections.Upsert(scope, section);
			schedules.Upsert(scope, schedule);
		});

		Assert.Equal([2, 4, 5], sections.Get(section.Id)!.MeetingDays);
		Assert.Equal(schedule.SectionIds, schedules.Get(schedule.ScheduleId)!.SectionIds);
		schedules.Get(schedule.ScheduleId)!.SectionIds[0] = Guid.Empty;
		Assert.Equal(schedule.SectionIds, schedules.Get(schedule.ScheduleId)!.SectionIds);
	}

	[Fact]
	public void a_case_insensitive_match_finds_display_cased_values() {
		var db = NewDb();
		var row = Row(course: "Chem 101");
		Write(db, row, Row(course: "BIO 110"));
		var store = db.Rows<Guid, SectionRow>();

		var found = store.FindOne(Match.Eq("term", "2026FA"), Match.IgnoringCase("course_code", "CHEM 101"), Match.Eq("section_number", 1));

		Assert.Equal(row.Id, found?.Id);
		Assert.Empty(store.FindBy(Match.Eq("course_code", "CHEM 101")));
		Assert.Null(store.FindOne(Match.IgnoringCase("course_code", "PHYS 201")));
	}

	[Fact]
	public void finding_by_many_values_matches_any_and_no_values_match_nothing() {
		var db = NewDb();
		var a = Row(number: 1);
		var b = Row(number: 2);
		Write(db, a, b, Row(number: 3));
		var store = db.Rows<Guid, SectionRow>();

		Assert.Equal(new[] { a.Id, b.Id }.Order(), store.FindIn("id", new[] { a.Id, b.Id, Guid.NewGuid() }).Select(r => r.Id).Order());
		Assert.Empty(store.FindIn("id", Array.Empty<Guid>()));
	}

	[Fact]
	public void naming_a_column_the_row_does_not_have_throws_before_any_sql() {
		var store = NewDb().Rows<Guid, SectionRow>();

		Assert.Throws<ArgumentException>(() => store.FindBy(Match.Eq("course", "CHEM 101")));
		Assert.Throws<ArgumentException>(() => store.FindIn("course", new[] { "CHEM 101" }));
	}

	[Fact]
	public void a_page_sorts_text_by_code_point_breaks_ties_by_key_and_counts_every_match() {
		var db = NewDb();
		// Mixed case on purpose: a locale collation would put "bio 110" first; code points put it last.
		var rows = new[] { Row("CHEM 101", 2), Row("bio 110", 3), Row("CHEM 101", 1), Row("BIO 110", 1), Row("BIO 110", 2) };
		Write(db, rows);
		var store = db.Rows<Guid, SectionRow>();
		var everything = new RowFilter<SectionRow>("TRUE", new Dictionary<string, object?>(), _ => true);
		var byCode = new[] { new OrderBy("course_code") };

		var first = store.Page(everything, byCode, 0, 3);
		var second = store.Page(everything, byCode, 3, 3);

		Guid[] Tied(string code) => rows.Where(r => r.CourseCode == code).Select(r => r.Id).Order().ToArray();
		var expected = Tied("BIO 110").Concat(Tied("CHEM 101")).Concat(Tied("bio 110")).ToArray();
		Assert.Equal(5, first.Total);
		Assert.Equal(expected[..3], first.Items.Select(r => r.Id));
		Assert.Equal(expected[3..], second.Items.Select(r => r.Id));
	}

	[Fact]
	public void a_transaction_that_throws_keeps_none_of_its_rows_or_checkpoints() {
		var db = NewDb();
		var store = db.Rows<Guid, SectionRow>();
		var kept = Row();
		Write(db, kept);
		var changed = store.Get(kept.Id)!;
		changed.SeatsHeld = 5;

		Assert.Throws<InvalidOperationException>(() => db.InTransaction(scope => {
			store.Upsert(scope, changed);
			store.Upsert(scope, Row(number: 2));
			store.Delete(scope, kept.Id);
			db.Checkpoints.Save(scope, "p", [new StreamCheckpoint("s", 4)]);
			throw new InvalidOperationException("before commit");
		}));

		Assert.Equal(0, Assert.Single(store.All()).SeatsHeld);
		Assert.Empty(db.Checkpoints.Load("p"));
	}

	[Fact]
	public void checkpoints_are_kept_per_stream_never_move_back_and_skip_streams_with_nothing_applied() {
		var db = NewDb();

		db.InTransaction(scope => db.Checkpoints.Save(scope, "p", [new StreamCheckpoint("a", 5), new StreamCheckpoint("b", null)]));
		db.InTransaction(scope => db.Checkpoints.Save(scope, "p", [new StreamCheckpoint("a", 3), new StreamCheckpoint("c", 0)]));
		db.InTransaction(scope => db.Checkpoints.Save(scope, "other", [new StreamCheckpoint("a", 1)]));

		var saved = db.Checkpoints.Load("p").ToDictionary(c => c.StreamName, c => c.Version);
		Assert.Equal(new Dictionary<string, long?> { ["a"] = 5, ["c"] = 0 }, saved);
	}
}

// ReSharper disable once InconsistentNaming
public sealed class when_storing_rows_in_memory : when_storing_rows {
	protected override IProjectionDb NewDb() => new InMemoryProjectionDb();

	[Fact]
	public void two_rows_on_one_natural_key_make_find_one_throw() {
		var db = NewDb();
		var store = db.Rows<Guid, SectionRow>();
		db.InTransaction(scope => {
			store.Upsert(scope, new SectionRow { Id = Guid.NewGuid(), Term = "2026FA", CourseCode = "CHEM 101", SectionNumber = 1 });
			store.Upsert(scope, new SectionRow { Id = Guid.NewGuid(), Term = "2026FA", CourseCode = "chem 101", SectionNumber = 1 });
		});

		Assert.Throws<InvalidOperationException>(() =>
			store.FindOne(Match.Eq("term", "2026FA"), Match.IgnoringCase("course_code", "CHEM 101"), Match.Eq("section_number", 1)));
	}
}

[Collection(PostgresCollection.Name)]
// ReSharper disable once InconsistentNaming
public sealed class when_storing_rows_in_postgres(PostgresFixture postgres) : when_storing_rows {
	protected override IProjectionDb NewDb() => new PostgresProjectionDb(postgres.NewDatabase());

	/// <summary>Postgres does not let the duplicate in: the expression index is the natural key, case and all.</summary>
	[Fact]
	public void the_natural_key_index_refuses_a_second_row_differing_only_in_case() {
		var db = NewDb();
		var store = db.Rows<Guid, SectionRow>();
		db.InTransaction(scope => store.Upsert(scope, new SectionRow { Id = Guid.NewGuid(), Term = "2026FA", CourseCode = "CHEM 101", SectionNumber = 1 }));

		var refused = Assert.Throws<Npgsql.PostgresException>(() => db.InTransaction(scope =>
			store.Upsert(scope, new SectionRow { Id = Guid.NewGuid(), Term = "2026FA", CourseCode = "chem 101", SectionNumber = 1 })));

		Assert.Equal(Npgsql.PostgresErrorCodes.UniqueViolation, refused.SqlState);
		Assert.Equal("sections_natural_key", refused.ConstraintName);
	}
}
