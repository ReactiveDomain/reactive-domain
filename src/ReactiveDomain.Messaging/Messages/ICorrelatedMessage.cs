using System;

namespace ReactiveDomain.Messaging
{
    public interface ICorrelatedMessage : IMessage
    {
        Guid CorrelationId { get; set; }
        Guid CausationId { get; set; }
    }
}