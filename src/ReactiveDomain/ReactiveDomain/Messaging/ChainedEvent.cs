using System;
using System.Threading;
using ReactiveDomain.Messages;

namespace ReactiveDomain.Messaging
{
    public class ChainedEvent : DomainEvent, IChainedMessage
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

        public override int MsgTypeId => TypeId;

        public Guid PrincipalId { get; }
        public readonly ChainSource Source;

        protected ChainedEvent(IChainedMessage source) :
            base(source.CorrelationId, source.MsgId)
        {
            PrincipalId = source.PrincipalId;
            Source = source.GetMemento();
        }
        protected ChainedEvent(Guid correlationId, Guid sourceId, Guid PrincipalId) :
            base(correlationId, sourceId)
        {
            PrincipalId = PrincipalId;
        }
    }

   
}
