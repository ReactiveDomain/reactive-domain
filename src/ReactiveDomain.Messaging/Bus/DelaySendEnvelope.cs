namespace ReactiveDomain.Messaging.Bus;

public record DelaySendEnvelope(TimePosition At, IMessage ToSend) : IMessage {
	public Guid MsgId { get; private set; } = Guid.NewGuid();

	public DelaySendEnvelope(ITimeSource timeSource, TimeSpan delay, IMessage toSend) :
		this(timeSource.Now() + delay, toSend) { }
}
