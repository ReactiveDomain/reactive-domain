using ReactiveDomain.Foundation;

namespace ReactiveDomain.ReadModels.Tests;

/// <summary>
/// <see cref="ProjectionReadModelBase"/> end to end, against each database: events through RD's
/// in-memory stream store, rows and checkpoints into the database, and restarts that resume from
/// what was saved.
/// </summary>
// ReSharper disable once InconsistentNaming
public abstract class when_projecting_counts : IDisposable {
	private readonly Func<IProjectionDb> _newDb;
	private Harness? _harness;

	// Built on first use inside the test, so a skipped database skips the test rather than failing construction.
	private Harness H => _harness ??= new Harness(_newDb());

	protected when_projecting_counts(Func<IProjectionDb> newDb) => _newDb = newDb;

	public void Dispose() => _harness?.Dispose();

	private long? Saved(string projection) =>
		H.Db.Checkpoints.Load(projection).SingleOrDefault(c => c.StreamName == Category())?.Version;

	private string Category() => H.Connection.StreamNamer.GenerateForCategory(typeof(ThingAgg));

	private int Written(Guid thing) => H.Db.Rows<Guid, ThingCountRow>().Get(thing)?.Total ?? 0;

	// CatchUp waits on the model's own checkpoints; the flush that saves them commits just after,
	// so a test that reads the saved checkpoint waits for the write itself.
	private void SavedReachesEnd(string projection) =>
		Harness.Eventually(() => Saved(projection) == H.LastEventIn(Category()), "the checkpoint was not written");

	[SkippableFact]
	public void history_already_written_is_caught_up_and_saved_in_one_commit() {
		var thing = H.Count();
		for (var i = 0; i < 4; i++)
			H.Count(thing);
		var counting = new FaultyDb(H.Db);

		var rm = H.Start(new CountsRM(H.Connection, counting));
		H.CatchUp(rm);

		Assert.Equal(1, counting.Commits);
		Assert.Equal(5, Written(thing));
		SavedReachesEnd(rm.Name);
	}

	/// <summary>
	/// A restart reads only what follows the saved checkpoint. Rereading from the start would add
	/// every event to a total that already holds it.
	/// </summary>
	[SkippableFact]
	public void a_restarted_projection_resumes_after_its_checkpoint_without_counting_twice() {
		var first = H.Start(new CountsRM(H.Connection, H.Db));
		var thing = H.Count(by: 2);
		H.Count(thing, by: 3);
		H.CatchUp(first);
		first.Dispose();
		H.Count(thing, by: 4);

		var second = H.Start(new CountsRM(H.Connection, H.Db));
		H.CatchUp(second);

		Assert.Equal(9, Written(thing));
		SavedReachesEnd(second.Name);
	}

	/// <summary>The rows and the checkpoint commit together or not at all; the buffered change is written by the next flush.</summary>
	[SkippableFact]
	public void a_write_that_throws_saves_neither_rows_nor_checkpoint_and_the_next_write_saves_both() {
		var faulty = new FaultyDb(H.Db);
		var rm = H.Start(new CountsRM(H.Connection, faulty));
		var before = Saved(rm.Name);

		faulty.ThrowBeforeNextCommit();
		var thing = H.Count(by: 2);
		Harness.Eventually(() => rm.Idle && rm.Applied(thing) == 2, "the event was not handled");
		Assert.Equal(0, Written(thing));
		Assert.Equal(before, Saved(rm.Name));

		H.Count(thing, by: 3);
		H.CatchUp(rm);

		Harness.Eventually(() => Written(thing) == 5, "the buffered change was not written");
		SavedReachesEnd(rm.Name);
	}

	[SkippableFact]
	public void a_restart_after_a_write_that_threw_replays_the_event_it_lost() {
		var faulty = new FaultyDb(H.Db);
		var first = H.Start(new CountsRM(H.Connection, faulty));
		faulty.ThrowBeforeNextCommit();
		var thing = H.Count(by: 2);
		Harness.Eventually(() => first.Idle && first.Applied(thing) == 2, "the event was not handled");
		first.Dispose();

		var second = H.Start(new CountsRM(H.Connection, H.Db));
		H.CatchUp(second);

		Assert.Equal(2, Written(thing));
	}

	/// <summary>
	/// The queue would log the throw and apply the next event, whose flush would checkpoint past the
	/// one never applied. The projection stops instead, and its checkpoint stays put.
	/// </summary>
	[SkippableFact]
	public void a_handler_that_throws_while_live_stops_the_projection_at_its_last_written_event() {
		var rm = H.Start(new CountsRM(H.Connection, H.Db, explodeOn: 13));
		var thing = H.Count(by: 1);
		H.CatchUp(rm);
		SavedReachesEnd(rm.Name);
		var checkpoint = Saved(rm.Name);

		H.Count(thing, by: 13);
		H.Count(thing, by: 2);

		Harness.Eventually(() => rm.Fault is not null, "the projection did not stop");
		Assert.IsType<InvalidOperationException>(rm.Fault);
		Assert.Equal(1, Written(thing));
		Assert.Equal(checkpoint, Saved(rm.Name));
	}

	[SkippableFact]
	public void a_handler_that_throws_while_catching_up_writes_nothing_and_a_restart_replays_it_all() {
		var thing = H.Count(by: 1);
		H.Count(thing, by: 13);
		H.Count(thing, by: 2);

		var exploding = H.Start(new CountsRM(H.Connection, H.Db, explodeOn: 13));

		Assert.NotNull(exploding.Fault);
		Assert.Equal(0, Written(thing));
		Assert.Null(Saved(exploding.Name));
		exploding.Dispose();

		var replacement = H.Start(new CountsRM(H.Connection, H.Db));
		H.CatchUp(replacement);
		Assert.Null(replacement.Fault);
		Assert.Equal(16, Written(thing));
	}

	/// <summary>
	/// A rolling deploy runs two instances of one projection for a while. Starting the newer one claims
	/// the writer fence, so the older one's next flush keeps nothing — its rows could be behind the
	/// newer instance's — and it stops, while the claim holder carries on alone.
	/// </summary>
	[SkippableFact]
	public void a_projection_that_lost_its_claim_keeps_nothing_more_and_its_replacement_carries_on() {
		var thing = H.Count();
		var old = H.Start(new CountsRM(H.Connection, H.Db));
		H.CatchUp(old);

		var replacement = H.Start(new CountsRM(H.Connection, H.Db));
		H.Count(thing, by: 2);

		Harness.Eventually(() => old.Fault is FencedWriterException, "the old instance was not fenced");
		H.CatchUp(replacement);
		Assert.Null(replacement.Fault);
		Harness.Eventually(() => Written(thing) == 3, "the replacement did not write");
		SavedReachesEnd(replacement.Name);
	}

	/// <summary>The rows always trail the streams; the freshness check is how work that trusts them waits out the gap.</summary>
	[SkippableFact]
	public void freshness_tells_rows_still_catching_up_from_current_ones() {
		var freshness = new ProjectionFreshness(H.Connection, H.Db);
		Assert.True(freshness.CaughtUp(nameof(CountsRM), typeof(ThingAgg)));

		H.Count();
		Assert.False(freshness.CaughtUp(nameof(CountsRM), typeof(ThingAgg)));

		var rm = H.Start(new CountsRM(H.Connection, H.Db));
		H.CatchUp(rm);
		Assert.True(freshness.CaughtUp(nameof(CountsRM), typeof(ThingAgg)));
	}
}

// ReSharper disable once InconsistentNaming
public sealed class when_projecting_counts_in_memory() : when_projecting_counts(() => new InMemoryProjectionDb());

[Collection(PostgresCollection.Name)]
// ReSharper disable once InconsistentNaming
public sealed class when_projecting_counts_in_postgres(PostgresFixture postgres)
	: when_projecting_counts(() => new PostgresProjectionDb(postgres.NewDatabase()));
