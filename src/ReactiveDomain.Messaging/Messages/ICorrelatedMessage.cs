using System;

namespace ReactiveDomain.Messaging.Messages
{
    public interface ICorrelatedMessage 
    {
        Guid MsgId { get; }
        SourceId SourceId { get; }
        CorrelationId CorrelationId { get; }
    }
}
