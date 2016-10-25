using System;

namespace ReactiveDomain.Messages
{
    public interface ICorrelatedMessage 
    {
        Guid MsgId { get; }
        Guid? SourceId { get; }
        Guid CorrelationId { get; }
    }
}
