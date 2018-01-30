using System;

namespace ReactiveDomain.Messages
{
    public interface IChainedMessage : ICorrelatedMessage
    {
        Guid PrincipalId { get; }
    }
}
