using System.Diagnostics.CodeAnalysis;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Foundation;

public interface ICorrelatedRepository {
	/// <summary>Loads the aggregate armed with <paramref name="source"/>, or returns false when none lives at <paramref name="id"/>: the stream does not exist or was deleted.</summary>
	/// <remarks>Only those two outcomes are false. A fault — the store unreachable, an event that no longer deserializes — propagates, so an outage cannot read as "does not exist" and feed a try-then-create path a phantom aggregate.</remarks>
	bool TryGetById<TAggregate>(Guid id, [NotNullWhen(true)] out TAggregate? aggregate, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource;

	/// <summary>Loads the aggregate at <paramref name="version"/>; false means not found or deleted, as on the other overload.</summary>
	bool TryGetById<TAggregate>(Guid id, int version, [NotNullWhen(true)] out TAggregate? aggregate, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource;
	TAggregate GetById<TAggregate>(Guid id, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource;
	TAggregate GetById<TAggregate>(Guid id, int version, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource;
	/// <inheritdoc cref="IRepository.Save"/>
	StreamCheckpoint Save(IEventSource aggregate);

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
	/// <returns>The aggregate's stream at the version it has after the write, as reported by <see cref="Save"/>.</returns>
	StreamCheckpoint SaveAndContinue(IEventSource aggregate);

	void Delete(IEventSource aggregate);
	void HardDelete(IEventSource aggregate);
}
