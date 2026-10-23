using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using ReactiveDomain.Testing.EventStore;

namespace ReactiveDomain.ReadModels.Tests;

/// <summary>The one event the test domain has: <see cref="By"/> more for a thing.</summary>
public sealed record ThingCounted(Guid ThingId, int By) : Event;

/// <summary>A counter, so the tests have a category of streams to project.</summary>
public sealed class ThingAgg : AggregateRoot {
	private ThingAgg() {
		Register<ThingCounted>(e => Id = e.ThingId);
	}

	/// <summary>Starts a thing at <paramref name="by"/>.</summary>
	public ThingAgg(Guid id, int by, ICorrelatedMessage source) : this() {
		Source = source;
		Raise(new ThingCounted(id, by));
	}

	/// <summary>Counts <paramref name="by"/> more.</summary>
	public void Count(int by) => Raise(new ThingCounted(Id, by));
}

/// <summary>Projects each thing's running total into <c>thing_counts</c>.</summary>
[OwnedTables(typeof(ThingCountRow))]
internal class CountsRM : ProjectionReadModelBase, IHandle<ThingCounted> {
	private readonly IRowStore<Guid, ThingCountRow> _store;
	private readonly WriteBuffer<Guid, ThingCountRow> _counts;
	private readonly Dictionary<Guid, int> _totals = [];
	private readonly int? _explodeOn;

	/// <summary>Creates the projection; counting <paramref name="explodeOn"/> throws, standing in for a projection bug.</summary>
	public CountsRM(IConfiguredConnection connection, IProjectionDb db, int? explodeOn = null)
		: base(nameof(CountsRM), connection, db) {
		_explodeOn = explodeOn;
		_store = db.Rows<Guid, ThingCountRow>();
		_counts = OwnTable(_store);
		EventStream.Subscribe<ThingCounted>(this);
	}

	/// <summary>The in-memory total for <paramref name="thing"/>; applied, whether or not written yet.</summary>
	public int Applied(Guid thing) {
		lock (ReaderLock)
			return _totals.TryGetValue(thing, out var total) ? total : 0;
	}

	protected override void Load() {
		foreach (var row in _store.All())
			_totals[row.Id] = row.Total;
	}

	protected override IReadOnlyList<StreamStarter> Streams() => [Category<ThingAgg>()];

	void IHandle<ThingCounted>.Handle(ThingCounted e) {
		if (e.By == _explodeOn)
			throw new InvalidOperationException($"cannot count {e.By}");
		var total = _totals.TryGetValue(e.ThingId, out var t) ? t + e.By : e.By;
		_totals[e.ThingId] = total;
		_counts.Upsert(e.ThingId, new ThingCountRow { Id = e.ThingId, Total = total });
	}
}

/// <summary>RD's in-memory stream store, a repository over it, and whichever <see cref="IProjectionDb"/> a test passes.</summary>
internal sealed class Harness : IDisposable {
	private readonly List<IDisposable> _owned = [];

	public MockStreamStoreConnection Store { get; }
	public IConfiguredConnection Connection { get; }
	public CorrelatedStreamStoreRepository Repository { get; }
	public IProjectionDb Db { get; }

	public Harness(IProjectionDb? db = null) {
		Store = new MockStreamStoreConnection(nameof(Harness));
		Store.Connect();
		Connection = new ConfiguredConnection(Store, new PrefixedCamelCaseStreamNameBuilder("rm"), new JsonMessageSerializer());
		Repository = new CorrelatedStreamStoreRepository(Connection.StreamNamer, Store, Connection.Serializer);
		Db = db ?? new InMemoryProjectionDb();
	}

	private static ICorrelatedMessage Source() => new CorrelationSource(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

	/// <summary>Counts <paramref name="by"/> on a thing, creating it as needed, and returns its id.</summary>
	public Guid Count(Guid? thing = null, int by = 1) {
		var id = thing ?? Guid.NewGuid();
		if (Repository.TryGetById<ThingAgg>(id, out var existing, Source())) {
			existing.Count(by);
			Repository.Save(existing);
		} else {
			Repository.Save(new ThingAgg(id, by, Source()));
		}
		return id;
	}

	/// <summary>Waits until <paramref name="model"/> has applied every event in the category.</summary>
	public void CatchUp(ReadModelBase model) =>
		AssertEx.IsOrBecomesTrue(() => model.Idle && CaughtUp(model), TestTimeouts.ThrottleWaitFor, $"{model.GetType().Name} did not catch up");

	private bool CaughtUp(ReadModelBase model) {
		var delivered = model.GetCheckpoint().ToDictionary(c => c.StreamName, c => c.Version ?? -1);
		return delivered.All(c => c.Value == LastEventIn(c.Key));
	}

	public long LastEventIn(string stream) {
		try {
			return Store.ReadStreamForward(stream, 0, 4096).Events.Length - 1;
		} catch (StreamNotFoundException) {
			return -1;
		}
	}

	public static void Eventually(Func<bool> condition, string because) =>
		AssertEx.IsOrBecomesTrue(condition, TestTimeouts.ThrottleWaitFor, because);

	public static void Wait(Task task) => Assert.True(task.Wait(TestTimeouts.ThrottleWaitFor), "timed out starting");

	/// <summary>Starts <paramref name="model"/>, owned by the harness, and waits until it is live.</summary>
	public T Start<T>(T model) where T : ProjectionReadModelBase {
		Own(model);
		Wait(model.StartAsync());
		return model;
	}

	public T Own<T>(T disposable) where T : IDisposable {
		_owned.Add(disposable);
		return disposable;
	}

	public void Dispose() {
		for (var i = _owned.Count - 1; i >= 0; i--)
			_owned[i].Dispose();
		Store.Dispose();
	}
}
