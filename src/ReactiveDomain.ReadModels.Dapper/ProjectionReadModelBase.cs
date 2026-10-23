using System.Collections.Concurrent;
using System.Reflection;
using ReactiveDomain.Foundation;
using ReactiveDomain.Logging;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.ReadModels;

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
/// <para>A handler that throws stops the projection. RD's queue would log the exception and hand the
/// next event to the handlers, and that event's flush would then save a checkpoint past the event
/// that was never applied, losing it for good. Instead, everything still buffered is dropped, no later
/// event is applied, and nothing more is written, so the saved checkpoint stays at the last event whose
/// rows were written. <see cref="Fault"/> holds the exception; a restart replays from there.</para>
/// <para>One instance writes at a time. Starting claims the projection's writer fence, and every flush
/// asserts the claim in the rows' own transaction, so when a newer instance takes over — a rolling
/// deploy — the older one's late flush keeps nothing and it stops with <see cref="Fault"/> set,
/// instead of writing the older state it knew over newer rows.</para>
/// <para>The tables a projection writes are declared with <see cref="OwnedTablesAttribute"/> and the
/// ones it reads with <see cref="ReadsTablesAttribute"/>; <see cref="ProjectionStartup"/> uses them to
/// start owners first.</para>
/// </remarks>
public abstract class ProjectionReadModelBase : BufferedReadModelBase {
	private static readonly ILogger Log = LogManager.GetLogger(typeof(ProjectionReadModelBase).FullName!);

	private readonly IProjectionDb _db;
	private readonly IStreamNameBuilder _namer;
	private readonly GuardedStream _eventStream;
	private readonly List<Action> _clearBuffers = [];
	private readonly List<Action<IWriteScope>> _tableWriters = [];
	private readonly Guid _writer = Guid.NewGuid();
	private Dictionary<string, long> _saved = [];
	private volatile Exception? _fault;

	/// <summary>The projection's name, which its checkpoints are saved under.</summary>
	public string Name { get; }

	/// <summary>The exception that stopped the projection, or null while it is running.</summary>
	public Exception? Fault => _fault;

	/// <summary>Creates a projection named <paramref name="name"/> over <paramref name="db"/>.</summary>
	protected ProjectionReadModelBase(string name, IConfiguredConnection connection, IProjectionDb db) : base(name, connection) {
		Name = name;
		_db = db;
		_namer = connection.StreamNamer;
		_eventStream = new GuardedStream(base.EventStream, this);
	}

	/// <summary>Where handlers subscribe. A handler that throws stops the projection; see the class remarks.</summary>
	public new ISubscriber EventStream => _eventStream;

	/// <summary>Declares a table this projection writes, and returns the buffer its handlers write through.</summary>
	/// <exception cref="InvalidOperationException">The table is not listed in this projection's <see cref="OwnedTablesAttribute"/>.</exception>
	protected WriteBuffer<TKey, TRow> OwnTable<TKey, TRow>(IRowStore<TKey, TRow> store)
		where TKey : notnull where TRow : class, new() {
		if (!ProjectionTables.Owned(GetType()).Contains(typeof(TRow)))
			throw new InvalidOperationException(
				$"{GetType().Name} writes {typeof(TRow).Name} without declaring [OwnedTables(typeof({typeof(TRow).Name}))].");
		var buffer = CreateBuffer<TKey, TRow>();
		_clearBuffers.Add(buffer.Clear);
		_tableWriters.Add(scope => {
			foreach (var key in buffer.Deletes)
				store.Delete(scope, key);
			foreach (var row in buffer.Upserts.Values)
				store.Upsert(scope, row);
		});
		return buffer;
	}

	/// <summary>A table another projection owns, typed so this one cannot write it.</summary>
	/// <exception cref="InvalidOperationException">The table is not listed in this projection's <see cref="ReadsTablesAttribute"/>.</exception>
	protected IReadOnlyRowStore<TKey, TRow> ReadTable<TKey, TRow>()
		where TKey : notnull where TRow : class, new() {
		if (!ProjectionTables.Read(GetType()).Contains(typeof(TRow)))
			throw new InvalidOperationException(
				$"{GetType().Name} reads {typeof(TRow).Name} without declaring [ReadsTables(typeof({typeof(TRow).Name}))].");
		return _db.ReadOnlyRows<TKey, TRow>();
	}

	/// <summary>
	/// Loads what the tables already hold, then reads each category from after its saved checkpoint.
	/// Completes once the projection is live and its catch-up is written.
	/// </summary>
	/// <remarks>Check <see cref="Fault"/> after it completes: a projection stopped by a handler, or fenced, still goes live, writing nothing.</remarks>
	public Task StartAsync() {
		// Claimed before anything is read: the claim waits out any older writer's in-flight flush and
		// fences the rest, so the checkpoints and rows read below cannot change under this instance.
		_db.Writers.Claim(Name, _writer);
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
	protected sealed override void Flush(IReadOnlyList<StreamCheckpoint> checkpoints) {
		if (_fault is not null)
			return;
		try {
			_db.InTransaction(scope => {
				_db.Writers.Assert(scope, Name, _writer);
				foreach (var write in _tableWriters)
					write(scope);
				_db.Checkpoints.Save(scope, Name, checkpoints);
			});
		} catch (FencedWriterException ex) {
			// Not left to the ordinary retry: the fence is permanent for this instance, and the new
			// writer resumed from the saved checkpoints, so nothing here is owed to the database.
			_fault = ex;
			foreach (var clear in _clearBuffers)
				clear();
			Log.ErrorException(ex, "{0} lost its writer fence; it applies nothing more, and the instance that claimed it carries on.", Name);
		}
	}

	private void Stop(IMessage message, Exception ex) {
		if (_fault is not null)
			return;
		_fault = ex;
		// What is buffered includes the failed event's partial changes, and while catching up every event
		// since the saved checkpoint: none of it is written, and a restart replays all of it.
		foreach (var clear in _clearBuffers)
			clear();
		Log.ErrorException(ex, "{0} stopped on {1}; it applies nothing more, and a restart replays from its saved checkpoint.",
			Name, message.GetType().Name);
	}

	/// <summary>Stands between the base stream and each handler, so a throw stops the projection.</summary>
	private sealed class GuardedStream(ISubscriber inner, ProjectionReadModelBase model) : ISubscriber {
		// Unsubscribe matches on the handler, so each guard is kept against the handler it wraps.
		private readonly ConcurrentDictionary<(object Handler, Type Message), object> _guards = new();

		public IDisposable Subscribe<T>(IHandle<T> handler, bool includeDerived = true) where T : class, IMessage =>
			inner.Subscribe(Guard(handler), includeDerived);

		public IDisposable SubscribeToAll(IHandle<IMessage> handler) => inner.SubscribeToAll(Guard(handler));

		public void Unsubscribe<T>(IHandle<T> handler) where T : class, IMessage {
			if (_guards.TryRemove((handler, typeof(T)), out var guard))
				inner.Unsubscribe((IHandle<T>)guard);
		}

		public bool HasSubscriberFor<T>(bool includeDerived = false) where T : class, IMessage =>
			inner.HasSubscriberFor<T>(includeDerived);

		private IHandle<T> Guard<T>(IHandle<T> handler) where T : class, IMessage =>
			(IHandle<T>)_guards.GetOrAdd((handler, typeof(T)), _ => new Guarded<T>(handler, model));
	}

	private sealed class Guarded<T>(IHandle<T> handler, ProjectionReadModelBase model) : IHandle<T> where T : class, IMessage {
		public void Handle(T message) {
			if (model._fault is not null)
				return;
			try {
				handler.Handle(message);
			} catch (Exception ex) {
				model.Stop(message, ex);
			}
		}
	}
}

/// <summary>The tables a projection writes. A table has exactly one owner.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class OwnedTablesAttribute(params Type[] rows) : Attribute {
	/// <summary>The row types of the owned tables.</summary>
	public IReadOnlyList<Type> Rows { get; } = rows;
}

/// <summary>The tables a projection reads but another owns. The owner is started, and live, first.</summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ReadsTablesAttribute(params Type[] rows) : Attribute {
	/// <summary>The row types of the tables read.</summary>
	public IReadOnlyList<Type> Rows { get; } = rows;
}

/// <summary>What a projection type declares it owns and reads.</summary>
public static class ProjectionTables {
	/// <summary>The row types <paramref name="projection"/> owns.</summary>
	public static IReadOnlyList<Type> Owned(Type projection) =>
		projection.GetCustomAttribute<OwnedTablesAttribute>()?.Rows ?? [];

	/// <summary>The row types <paramref name="projection"/> reads from other owners.</summary>
	public static IReadOnlyList<Type> Read(Type projection) =>
		projection.GetCustomAttribute<ReadsTablesAttribute>()?.Rows ?? [];
}
