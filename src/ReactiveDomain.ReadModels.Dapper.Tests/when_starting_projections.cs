using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.ReadModels.Tests;

/// <summary>Declared table ownership, and starting projections so a reader's owners are live first.</summary>
// ReSharper disable once InconsistentNaming
public sealed class when_starting_projections : IDisposable {
	private readonly Harness _h = new();

	public void Dispose() => _h.Dispose();

	private T Own<T>(T projection) where T : IDisposable => _h.Own(projection);

	/// <summary>
	/// The reader counts the owner's table when it loads. Listed first, it would load before the
	/// owner caught up; the startup holds it until the owner is live.
	/// </summary>
	[Fact]
	public void a_reader_starts_only_once_the_owner_of_what_it_reads_is_live() {
		for (var i = 0; i < 5; i++)
			_h.Count();
		var reader = Own(new SummaryRM(_h.Connection, _h.Db));
		var owner = Own(new CountsRM(_h.Connection, _h.Db));

		Harness.Wait(ProjectionStartup.StartAllAsync([reader, owner]));

		Assert.Equal(5, reader.CountsAtLoad);
	}

	/// <summary>
	/// A faulted owner left a partial table; a reader that loaded it would bake that history into
	/// rows its own checkpoints then stand behind, and rebuilding the owner would not repair it.
	/// </summary>
	[Fact]
	public async Task a_faulted_owner_fails_its_readers_start_instead_of_feeding_them_partial_rows() {
		var thing = _h.Count(by: 1);
		_h.Count(thing, by: 13);
		var reader = Own(new SummaryRM(_h.Connection, _h.Db));
		var owner = Own(new CountsRM(_h.Connection, _h.Db, explodeOn: 13));

		var refused = await Assert.ThrowsAsync<InvalidOperationException>(() => ProjectionStartup.StartAllAsync([reader, owner]));

		Assert.Contains("stopped instead of catching up", refused.Message);
		Assert.NotNull(owner.Fault);
		Assert.Equal(-1, reader.CountsAtLoad);
	}

	[Fact]
	public void a_projection_named_apart_from_its_type_is_refused() =>
		Assert.Throws<ArgumentException>(() => new MisnamedRM(_h.Connection, _h.Db));

	[Fact]
	public void subscribing_one_handler_to_one_message_twice_is_refused() =>
		Assert.Throws<InvalidOperationException>(() => new DoubleSubscribedRM(_h.Connection, _h.Db));

	/// <summary>Detaching clears the guard's bookkeeping, so the same handler can subscribe again.</summary>
	[Fact]
	public void a_detached_handler_can_subscribe_again() {
		var rm = Own(new ResubscribingRM(_h.Connection, _h.Db));

		rm.Detach();
		rm.Attach();
		rm.Detach();
		rm.AttachDisposably().Dispose();
		rm.Attach();
	}

	[Fact]
	public void two_projections_owning_one_table_are_refused_before_either_starts() {
		var first = Own(new CountsRM(_h.Connection, _h.Db));
		var second = Own(new CountsRM(_h.Connection, _h.Db));

		var refused = Assert.Throws<InvalidOperationException>(() => { _ = ProjectionStartup.StartAllAsync([first, second]); });

		Assert.Contains("both own ThingCountRow", refused.Message);
	}

	[Fact]
	public void reading_a_table_no_projection_owns_is_refused() {
		var reader = Own(new SummaryRM(_h.Connection, _h.Db));

		var refused = Assert.Throws<InvalidOperationException>(() => { _ = ProjectionStartup.StartAllAsync([reader]); });

		Assert.Contains("which no projection owns", refused.Message);
	}

	[Fact]
	public void projections_reading_each_other_in_a_cycle_are_refused() {
		var a = Own(new CycleARM(_h.Connection, _h.Db));
		var b = Own(new CycleBRM(_h.Connection, _h.Db));

		var refused = Assert.Throws<InvalidOperationException>(() => { _ = ProjectionStartup.StartAllAsync([a, b]); });

		Assert.Contains("cycle", refused.Message);
	}

	[Fact]
	public void writing_or_reading_an_undeclared_table_is_refused_when_the_projection_is_built() {
		Assert.Throws<InvalidOperationException>(() => new UndeclaredWriterRM(_h.Connection, _h.Db));
		Assert.Throws<InvalidOperationException>(() => new UndeclaredReaderRM(_h.Connection, _h.Db));
	}

	[Table("cycle_a")]
	private sealed class CycleARow {
		[Key] public Guid Id { get; set; }
	}

	[Table("cycle_b")]
	private sealed class CycleBRow {
		[Key] public Guid Id { get; set; }
	}

	private abstract class TestProjection(string name, IConfiguredConnection connection, IProjectionDb db)
		: ProjectionReadModelBase(name, connection, db) {
		protected override void Load() { }

		protected override IReadOnlyList<StreamStarter> Streams() => [Category<ThingAgg>()];
	}

	[OwnedTables(typeof(ThingSummaryRow))]
	[ReadsTables(typeof(ThingCountRow))]
	private sealed class SummaryRM : TestProjection {
		private readonly IReadOnlyRowStore<Guid, ThingCountRow> _counts;

		public SummaryRM(IConfiguredConnection connection, IProjectionDb db) : base(nameof(SummaryRM), connection, db) {
			OwnTable(db.Rows<Guid, ThingSummaryRow>());
			_counts = ReadTable<Guid, ThingCountRow>();
		}

		public int CountsAtLoad { get; private set; } = -1;

		protected override void Load() => CountsAtLoad = _counts.All().Count;
	}

	[OwnedTables(typeof(CycleARow))]
	[ReadsTables(typeof(CycleBRow))]
	private sealed class CycleARM(IConfiguredConnection connection, IProjectionDb db) : TestProjection(nameof(CycleARM), connection, db);

	[OwnedTables(typeof(CycleBRow))]
	[ReadsTables(typeof(CycleARow))]
	private sealed class CycleBRM(IConfiguredConnection connection, IProjectionDb db) : TestProjection(nameof(CycleBRM), connection, db);

	private sealed class MisnamedRM(IConfiguredConnection connection, IProjectionDb db)
		: TestProjection("SomethingElse", connection, db);

	private sealed class DoubleSubscribedRM : TestProjection, IHandle<ThingCounted> {
		public DoubleSubscribedRM(IConfiguredConnection connection, IProjectionDb db) : base(nameof(DoubleSubscribedRM), connection, db) {
			EventStream.Subscribe<ThingCounted>(this);
			EventStream.Subscribe<ThingCounted>(this);
		}

		void IHandle<ThingCounted>.Handle(ThingCounted message) { }
	}

	private sealed class ResubscribingRM : TestProjection, IHandle<ThingCounted> {
		public ResubscribingRM(IConfiguredConnection connection, IProjectionDb db) : base(nameof(ResubscribingRM), connection, db) =>
			Attach();

		public void Attach() => EventStream.Subscribe<ThingCounted>(this);

		public IDisposable AttachDisposably() => EventStream.Subscribe<ThingCounted>(this);

		public void Detach() => EventStream.Unsubscribe<ThingCounted>(this);

		void IHandle<ThingCounted>.Handle(ThingCounted message) { }
	}

	private sealed class UndeclaredWriterRM : TestProjection {
		public UndeclaredWriterRM(IConfiguredConnection connection, IProjectionDb db) : base(nameof(UndeclaredWriterRM), connection, db) =>
			OwnTable(db.Rows<Guid, ThingCountRow>());
	}

	private sealed class UndeclaredReaderRM : TestProjection {
		public UndeclaredReaderRM(IConfiguredConnection connection, IProjectionDb db) : base(nameof(UndeclaredReaderRM), connection, db) =>
			ReadTable<Guid, ThingCountRow>();
	}
}
