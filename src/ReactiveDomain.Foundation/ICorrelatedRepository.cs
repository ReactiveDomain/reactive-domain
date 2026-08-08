using System.Diagnostics.CodeAnalysis;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Foundation;

public interface ICorrelatedRepository {
	bool TryGetById<TAggregate>(Guid id, [NotNullWhen(true)] out TAggregate? aggregate, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource;
	bool TryGetById<TAggregate>(Guid id, int version, [NotNullWhen(true)] out TAggregate? aggregate, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource;
	TAggregate GetById<TAggregate>(Guid id, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource;
	TAggregate GetById<TAggregate>(Guid id, int version, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource;
	void Save(IEventSource aggregate);

	/// <summary>
	/// Persists the recorded events and leaves the aggregate armed with the same source — the
	/// intermediate save for a handler that writes durable state and keeps raising in one unit of work.
	/// </summary>
	/// <remarks>
	/// <para><see cref="Save(IEventSource)"/> ends the unit of work: it clears the source, so a later
	/// raise on the held instance throws. Use this instead for every write of a multi-write handler
	/// except the last — the events of all of them carry the one command's correlation and causation.</para>
	/// <para>A subsequent <c>GetById</c> re-arms the instance for its own source either way, so
	/// cached-instance reuse across commands is unaffected.</para>
	/// </remarks>
	/// <param name="aggregate">The aggregate whose recorded events to persist.</param>
	void SaveAndContinue(IEventSource aggregate);

	void Delete(IEventSource aggregate);
	void HardDelete(IEventSource aggregate);
}
