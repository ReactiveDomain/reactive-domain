using System.Diagnostics.CodeAnalysis;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Foundation;

public interface ICorrelatedRepository {
	bool TryGetById<TAggregate>(Guid id, [NotNullWhen(true)] out TAggregate? aggregate, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource;
	bool TryGetById<TAggregate>(Guid id, int version, [NotNullWhen(true)] out TAggregate? aggregate, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource;
	TAggregate GetById<TAggregate>(Guid id, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource;
	TAggregate GetById<TAggregate>(Guid id, int version, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource;
	void Save(IEventSource aggregate);
	void Delete(IEventSource aggregate);
	void HardDelete(IEventSource aggregate);
}
