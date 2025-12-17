using System;

namespace ReactiveDomain.Messaging;

public class MessageBuilder {
    private Guid? _correlationId;
    private Guid? _causationId;
    public static MessageBuilder From(ICorrelatedMessage message) {
        return new MessageBuilder {
            _correlationId = message.CorrelationId,
            _causationId = message.MsgId
        };
    }
    public T Build<T>(Func<T> messageConstructor) where T : ICorrelatedMessage {
        var msg = messageConstructor();
        msg.CorrelationId = _correlationId ?? msg.MsgId;
        msg.CausationId = _causationId ?? Guid.Empty;
        return msg;
    }
    public static T New<T>(Func<T> messageConstructor) where T : ICorrelatedMessage {
        return new MessageBuilder().Build(messageConstructor);
    }
}