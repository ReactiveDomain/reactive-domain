using ReactiveDomain.Messaging;

namespace ReactiveDomain.Testing;

public record WoftamEvent(string Property1, string Property2) : IEvent {
	public Guid MsgId { get; private set; } = Guid.NewGuid();
}
