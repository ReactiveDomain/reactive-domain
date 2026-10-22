using CourseRegistration.ReadModels.Persistence;
using ReactiveDomain;
using ReactiveDomain.Foundation;

namespace CourseRegistration.ReadModels;

/// <summary>
/// A read model that keeps its tables in an <see cref="IProjectionDb"/> and resumes from the
/// checkpoints saved with them.
/// </summary>
/// <remarks>
/// <para>Handlers change rows in memory and record them on a table's buffer; nothing is written while
/// catching up. <see cref="BufferedReadModelBase"/> then calls <see cref="Flush"/> once at the live
/// transition and once per event after that, with the checkpoints of exactly what was applied, and
/// the rows and those checkpoints commit in one transaction. A restart therefore resumes after the
/// last event whose rows were written, never before it (counting an event twice) or after it
/// (losing one).</para>
/// <para>A flush that throws keeps everything buffered, and the next one writes it.</para>
/// </remarks>
public abstract class ProjectionRM : BufferedReadModelBase {
	private readonly IProjectionDb _db;
	private readonly IStreamNameBuilder _namer;
	private readonly List<Action<IWriteScope>> _tableWriters = [];
	private Dictionary<string, long> _saved = [];

	/// <summary>The projection's name, which its checkpoints are saved under.</summary>
	public string Name { get; }

	/// <summary>Creates a projection named <paramref name="name"/> over <paramref name="db"/>.</summary>
	protected ProjectionRM(string name, IConfiguredConnection connection, IProjectionDb db) : base(name, connection) {
		Name = name;
		_db = db;
		_namer = connection.StreamNamer;
	}

	/// <summary>The database this projection writes to.</summary>
	protected IProjectionDb Db => _db;

	/// <summary>Declares a table this projection owns, and returns the buffer its handlers write through.</summary>
	protected WriteBuffer<TKey, TRow> OwnTable<TKey, TRow>(IRowStore<TKey, TRow> store)
		where TKey : notnull where TRow : class, new() {
		var buffer = CreateBuffer<TKey, TRow>();
		_tableWriters.Add(scope => {
			foreach (var key in buffer.Deletes)
				store.Delete(scope, key);
			foreach (var row in buffer.Upserts.Values)
				store.Upsert(scope, row);
		});
		return buffer;
	}

	/// <summary>
	/// Loads what the tables already hold, then reads each category from after its saved checkpoint.
	/// Completes once the projection is live and its catch-up is written.
	/// </summary>
	public Task StartAsync() {
		_saved = _db.Checkpoints.Load(Name)
			.Where(c => c.Version is not null)
			.ToDictionary(c => c.StreamName, c => c.Version!.Value);
		Load();
		return StartAllAsync(Streams());
	}

	/// <summary>Fills the in-memory state from the tables. Runs before any stream is read.</summary>
	protected abstract void Load();

	/// <summary>The categories this projection reads, built with <see cref="Category{TAggregate}"/>.</summary>
	protected abstract IReadOnlyList<StreamStarter> Streams();

	/// <summary>The category of <typeparamref name="TAggregate"/>, resumed after its saved checkpoint.</summary>
	protected StreamStarter Category<TAggregate>() where TAggregate : class, IEventSource {
		var stream = _namer.GenerateForCategory(typeof(TAggregate));
		return StreamStarter.ForCategory<TAggregate>(_saved.TryGetValue(stream, out var version) ? version : null);
	}

	/// <inheritdoc/>
	protected sealed override void Flush(IReadOnlyList<StreamCheckpoint> checkpoints) =>
		_db.InTransaction(scope => {
			foreach (var write in _tableWriters)
				write(scope);
			_db.Checkpoints.Save(scope, Name, checkpoints);
		});
}
