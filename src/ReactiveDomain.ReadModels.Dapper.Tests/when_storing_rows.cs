namespace ReactiveDomain.ReadModels.Tests;

/// <summary>
/// What every <see cref="IProjectionDb"/> must do, run against each one. A behaviour that differs
/// between memory and Postgres is caught here rather than in production.
/// </summary>
// ReSharper disable once InconsistentNaming
public abstract class when_storing_rows {
	protected abstract IProjectionDb NewDb();

	private static OrderRow Row(string user = "Pat", string url = "https://example.test/a", int credit = 0) =>
		new() { Id = Guid.NewGuid(), User = user, URL = url, Credit = credit };

	private static void Write(IProjectionDb db, params OrderRow[] rows) {
		var store = db.Rows<Guid, OrderRow>();
		db.InTransaction(scope => {
			foreach (var row in rows)
				store.Upsert(scope, row);
		});
	}

	[Theory]
	[InlineData("Credit", "credit")]
	[InlineData("User", "user")]
	[InlineData("URL", "url")]
	[InlineData("TagsJson", "tags")]
	public void a_property_snake_cases_with_acronyms_kept_whole(string property, string column) =>
		Assert.Equal(column, RowMap<OrderRow>.Columns.Single(c => c.Property.Name == property).Name);

	[SkippableFact]
	public void an_upsert_replaces_the_row_and_a_read_returns_a_copy() {
		var db = NewDb();
		var store = db.Rows<Guid, OrderRow>();
		var row = Row();
		Write(db, row);
		row.Credit = 7;
		Assert.Equal(0, store.Get(row.Id)!.Credit);

		Write(db, row);

		var read = store.Get(row.Id)!;
		Assert.Equal(7, read.Credit);
		read.Credit = 99;
		Assert.Equal(7, store.Get(row.Id)!.Credit);
		Assert.Single(store.All());
	}

	[SkippableFact]
	public void an_insert_never_overwrites_and_says_whether_it_wrote() {
		var db = NewDb();
		var store = db.Rows<Guid, OrderRow>();
		var row = Row();
		var changed = Row(user: "Sam");
		changed.Id = row.Id;
		bool first = false, second = true;

		db.InTransaction(scope => first = store.Insert(scope, row));
		db.InTransaction(scope => second = store.Insert(scope, changed));

		Assert.True(first);
		Assert.False(second);
		Assert.Equal("Pat", store.Get(row.Id)!.User);
	}

	[SkippableFact]
	public void deleting_removes_the_row_and_deleting_again_is_harmless() {
		var db = NewDb();
		var store = db.Rows<Guid, OrderRow>();
		var row = Row();
		Write(db, row);

		db.InTransaction(scope => store.Delete(scope, row.Id));
		db.InTransaction(scope => store.Delete(scope, row.Id));

		Assert.Null(store.Get(row.Id));
	}

	/// <summary>
	/// A plain read sees committed rows only — in Postgres it runs on its own connection — while the
	/// scoped read sees the transaction's own writes. Pinned here because an in-memory store that let
	/// plain reads see uncommitted writes would pass tests that then read stale rows in Postgres.
	/// </summary>
	[SkippableFact]
	public void a_plain_read_sees_committed_rows_and_a_scoped_read_sees_the_transactions_own() {
		var db = NewDb();
		var store = db.Rows<Guid, OrderRow>();
		var row = Row();
		Write(db, row);
		var changed = Row();
		changed.Id = row.Id;
		changed.Credit = 5;

		db.InTransaction(scope => {
			store.Upsert(scope, changed);
			Assert.Equal(0, store.Get(row.Id)!.Credit);
			Assert.Equal(5, store.Get(scope, row.Id)!.Credit);
		});

		Assert.Equal(5, store.Get(row.Id)!.Credit);
	}

	[SkippableFact]
	public void a_scoped_read_sees_the_transactions_own_delete_and_insert() {
		var db = NewDb();
		var store = db.Rows<Guid, OrderRow>();
		var row = Row();
		Write(db, row);

		db.InTransaction(scope => {
			store.Delete(scope, row.Id);
			Assert.Null(store.Get(scope, row.Id));
			Assert.NotNull(store.Get(row.Id));
			Assert.True(store.Insert(scope, row));
		});

		Assert.NotNull(store.Get(row.Id));
	}

	[SkippableFact]
	public void a_fenced_writers_transaction_keeps_neither_rows_nor_checkpoint() {
		var db = NewDb();
		var store = db.Rows<Guid, OrderRow>();
		var old = Guid.NewGuid();
		db.Writers.Claim("counts", old);
		var row = Row();
		db.InTransaction(scope => {
			db.Writers.Assert(scope, "counts", old);
			store.Upsert(scope, row);
		});

		db.Writers.Claim("counts", Guid.NewGuid());

		var late = Row();
		late.Id = row.Id;
		late.Credit = 9;
		// The assert comes last here: the fence voids the whole transaction, not just what follows it.
		Assert.Throws<FencedWriterException>(() => db.InTransaction(scope => {
			store.Upsert(scope, late);
			db.Checkpoints.Save(scope, "counts", [new StreamCheckpoint("stream", 5)]);
			db.Writers.Assert(scope, "counts", old);
		}));
		Assert.Equal(0, store.Get(row.Id)!.Credit);
		Assert.Empty(db.Checkpoints.Load("counts"));
	}

	[SkippableFact]
	public void a_jsonb_column_round_trips() {
		var db = NewDb();
		var row = Row();
		row.TagsJson = """["a", "b"]""";
		Write(db, row);

		Assert.Equal("""["a", "b"]""", db.Rows<Guid, OrderRow>().Get(row.Id)!.TagsJson);
	}

	[SkippableFact]
	public void array_columns_round_trip_and_a_read_never_shares_one() {
		var db = NewDb();
		var store = db.Rows<Guid, OrderRow>();
		var row = Row();
		row.Sizes = [2, 4, 5];
		Write(db, row);

		Assert.Equal([2, 4, 5], store.Get(row.Id)!.Sizes);
		store.Get(row.Id)!.Sizes[0] = 99;
		Assert.Equal([2, 4, 5], store.Get(row.Id)!.Sizes);
	}

	[SkippableFact]
	public void a_case_insensitive_match_finds_display_cased_values() {
		var db = NewDb();
		var row = Row(user: "Pat");
		Write(db, row, Row(user: "Sam"));
		var store = db.Rows<Guid, OrderRow>();

		var found = store.FindOne(Match.IgnoringCase("user", "PAT"));

		Assert.Equal(row.Id, found?.Id);
		Assert.Empty(store.FindBy(Match.Eq("user", "PAT")));
		Assert.Null(store.FindOne(Match.IgnoringCase("user", "Alex")));
	}

	[SkippableFact]
	public void finding_by_many_values_matches_any_and_no_values_match_nothing() {
		var db = NewDb();
		var a = Row();
		var b = Row();
		Write(db, a, b, Row());
		var store = db.Rows<Guid, OrderRow>();

		Assert.Equal(new[] { a.Id, b.Id }.Order(), store.FindIn("id", new[] { a.Id, b.Id, Guid.NewGuid() }).Select(r => r.Id).Order());
		Assert.Empty(store.FindIn("id", Array.Empty<Guid>()));
	}

	[SkippableFact]
	public void naming_a_column_the_row_does_not_have_throws_before_any_sql() {
		var store = NewDb().Rows<Guid, OrderRow>();

		Assert.Throws<ArgumentException>(() => store.FindBy(Match.Eq("username", "Pat")));
		Assert.Throws<ArgumentException>(() => store.FindIn("username", new[] { "Pat" }));
	}

	[SkippableFact]
	public void a_page_sorts_text_by_code_point_breaks_ties_by_key_and_counts_every_match() {
		var db = NewDb();
		// Mixed case on purpose: a locale collation would put "bravo" first; code points put it last.
		var rows = new[] { Row(user: "CHARLIE"), Row(user: "bravo"), Row(user: "CHARLIE"), Row(user: "BRAVO"), Row(user: "BRAVO") };
		Write(db, rows);
		var store = db.Rows<Guid, OrderRow>();
		var everything = new RowFilter<OrderRow>("TRUE", new Dictionary<string, object?>(), _ => true);
		var byUser = new[] { new OrderBy("user") };

		var first = store.Page(everything, byUser, 0, 3);
		var second = store.Page(everything, byUser, 3, 3);

		Guid[] Tied(string user) => rows.Where(r => r.User == user).Select(r => r.Id).Order().ToArray();
		var expected = Tied("BRAVO").Concat(Tied("CHARLIE")).Concat(Tied("bravo")).ToArray();
		Assert.Equal(5, first.Total);
		Assert.Equal(expected[..3], first.Items.Select(r => r.Id));
		Assert.Equal(expected[3..], second.Items.Select(r => r.Id));
	}

	/// <summary>The SQL half and the predicate half of one filter keep the same rows.</summary>
	[SkippableFact]
	public void a_filter_keeps_the_rows_both_its_halves_agree_on() {
		var db = NewDb();
		var kept = Row(credit: 5);
		Write(db, kept, Row(credit: 1));
		var store = db.Rows<Guid, OrderRow>();

		var filter = new RowFilter<OrderRow>(
			"credit >= @Min",
			new Dictionary<string, object?> { ["Min"] = 3 },
			r => r.Credit >= 3);

		Assert.Equal([kept.Id], store.Find(filter).Select(r => r.Id));
	}

	[SkippableFact]
	public void a_transaction_that_throws_keeps_none_of_its_rows_or_checkpoints() {
		var db = NewDb();
		var store = db.Rows<Guid, OrderRow>();
		var kept = Row();
		Write(db, kept);
		var changed = store.Get(kept.Id)!;
		changed.Credit = 5;

		Assert.Throws<InvalidOperationException>(() => db.InTransaction(scope => {
			store.Upsert(scope, changed);
			store.Upsert(scope, Row());
			store.Delete(scope, kept.Id);
			db.Checkpoints.Save(scope, "p", [new StreamCheckpoint("s", 4)]);
			throw new InvalidOperationException("before commit");
		}));

		Assert.Equal(0, Assert.Single(store.All()).Credit);
		Assert.Empty(db.Checkpoints.Load("p"));
	}

	[SkippableFact]
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
	public void two_matches_make_find_one_throw() {
		var db = NewDb();
		var store = db.Rows<Guid, OrderRow>();
		db.InTransaction(scope => {
			store.Upsert(scope, new OrderRow { Id = Guid.NewGuid(), User = "Pat" });
			store.Upsert(scope, new OrderRow { Id = Guid.NewGuid(), User = "pat" });
		});

		Assert.Throws<InvalidOperationException>(() => store.FindOne(Match.IgnoringCase("user", "PAT")));
	}
}

[Collection(PostgresCollection.Name)]
// ReSharper disable once InconsistentNaming
public sealed class when_storing_rows_in_postgres(PostgresFixture postgres) : when_storing_rows {
	protected override IProjectionDb NewDb() => new PostgresProjectionDb(postgres.NewDatabase());
}
