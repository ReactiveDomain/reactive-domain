using System;
using System.Threading;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Messages;

namespace ReactiveDomain.PrivateLedger
{
    public class ChainedEvent : Event, IChainedMessage
    {
        public Guid PrincipalId { get; }
        public readonly ChainSource Source;

        protected ChainedEvent(IChainedMessage source) :
            base(source.CorrelationId,source.SourceId)
        {
            PrincipalId = source.PrincipalId;
            Source = source.GetMemento();
        }
        protected ChainedEvent(CorrelatedMessage source, Guid principalId) :
            base(source)
        {
            PrincipalId = principalId;
        }
    }

   
}
