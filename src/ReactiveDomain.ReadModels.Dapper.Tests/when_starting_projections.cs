using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ReactiveDomain.Foundation;

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

	private sealed class UndeclaredWriterRM : TestProjection {
		public UndeclaredWriterRM(IConfiguredConnection connection, IProjectionDb db) : base(nameof(UndeclaredWriterRM), connection, db) =>
			OwnTable(db.Rows<Guid, ThingCountRow>());
	}

	private sealed class UndeclaredReaderRM : TestProjection {
		public UndeclaredReaderRM(IConfiguredConnection connection, IProjectionDb db) : base(nameof(UndeclaredReaderRM), connection, db) =>
			ReadTable<Guid, ThingCountRow>();
	}
}
