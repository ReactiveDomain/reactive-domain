using System;

namespace ReactiveDomain.Messaging
{
    public interface IChainedMessage : ICorrelatedMessage
    {
        Guid PrincipalId { get; }
    }
}