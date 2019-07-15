using System;

namespace ReactiveDomain.Messaging.Bus {
    public class DelaySendEnvelope : IMessage {
        public Guid MsgId { get; private set; }
        public readonly TimePosition At;
        public readonly IMessage ToSend;

        public DelaySendEnvelope(TimePosition at, IMessage toSend) {
            MsgId = Guid.NewGuid();
            At = at;
            ToSend = toSend;
        }
        public DelaySendEnvelope(ITimeSource timeSource, TimeSpan delay, IMessage toSend) : 
            this(timeSource.Now() + delay, toSend) {}
    }
}