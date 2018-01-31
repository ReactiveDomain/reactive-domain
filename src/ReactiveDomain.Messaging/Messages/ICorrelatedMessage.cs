using System;

namespace ReactiveDomain.Messaging.Messages
{
    public interface ICorrelatedMessage 
    {
        Guid MsgId { get; }
        Guid? SourceId { get; }
        Guid CorrelationId { get; }
    }
}
