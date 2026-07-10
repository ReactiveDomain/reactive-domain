using ReactiveDomain.Messaging;

namespace ReactiveDomain.Testing;

public record TestWoftamAggregateCreated(Guid AggregateId) : IEvent {
	public Guid MsgId { get; private set; } = Guid.NewGuid();
}
