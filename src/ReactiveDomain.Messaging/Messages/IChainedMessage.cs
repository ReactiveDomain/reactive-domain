using System;

namespace ReactiveDomain.Messaging.Messages
{
    public interface IChainedMessage : ICorrelatedMessage
    {
        Guid PrincipalId { get; }
    }
}