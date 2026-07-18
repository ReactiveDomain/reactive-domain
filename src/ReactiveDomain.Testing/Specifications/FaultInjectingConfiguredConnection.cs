using System.Diagnostics.CodeAnalysis;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Testing;

/// <summary>
/// Thrown by repositories handed out by <see cref="FaultInjectingConfiguredConnection"/> when the
/// fault predicate matches a <see cref="IEventSource"/> being saved.
/// </summary>
public sealed class InjectedSaveException(IEventSource aggregate)
	: Exception($"Injected save failure for {aggregate.GetType().Name} {aggregate.Id}") {
	public IEventSource Aggregate { get; } = aggregate;
}

/// <summary>
/// An <see cref="IConfiguredConnection"/> decorator for deterministically exercising save-failure
/// and compensation paths: repositories it hands out throw <see cref="InjectedSaveException"/>
/// from Save when <paramref name="shouldFail"/> returns true for the aggregate being saved.
/// Reads, listeners, and readers pass through untouched.
/// </summary>
public sealed class FaultInjectingConfiguredConnection(
	IConfiguredConnection inner,
	Func<IEventSource, bool> shouldFail) : IConfiguredConnection {
	public IStreamStoreConnection Connection => inner.Connection;
	public IStreamNameBuilder StreamNamer => inner.StreamNamer;
	public IEventSerializer Serializer => inner.Serializer;

	public IListener GetListener(string name) => inner.GetListener(name);
	public IListener GetQueuedListener(string name) => inner.GetQueuedListener(name);
	public IStreamReader GetReader(string name, Action<IMessage> handle) => inner.GetReader(name, handle);

	public IRepository GetRepository(bool caching = false, Func<Guid>? currentPolicyUserId = null) =>
		new FaultInjectingRepository(inner.GetRepository(caching, currentPolicyUserId), shouldFail);

	public ICorrelatedRepository GetCorrelatedRepository(
		IRepository? baseRepository = null, bool caching = false, Func<Guid>? currentPolicyUserId = null) =>
		inner.GetCorrelatedRepository(baseRepository ?? GetRepository(caching, currentPolicyUserId));

	private sealed class FaultInjectingRepository(IRepository inner, Func<IEventSource, bool> shouldFail) : IRepository {
		public bool TryGetById<TAggregate>(Guid id, [NotNullWhen(true)] out TAggregate? aggregate, int version = int.MaxValue)
			where TAggregate : class, IEventSource => inner.TryGetById(id, out aggregate, version);

		public TAggregate GetById<TAggregate>(Guid id, int version = int.MaxValue)
			where TAggregate : class, IEventSource => inner.GetById<TAggregate>(id, version);

		public void Update<TAggregate>(ref TAggregate aggregate, int version = int.MaxValue)
			where TAggregate : class, IEventSource => inner.Update(ref aggregate, version);

		public void Save(IEventSource aggregate) {
			if (shouldFail(aggregate)) { throw new InjectedSaveException(aggregate); }
			inner.Save(aggregate);
		}

		public void Delete(IEventSource aggregate) => inner.Delete(aggregate);
		public void HardDelete(IEventSource aggregate) => inner.HardDelete(aggregate);
	}
}
