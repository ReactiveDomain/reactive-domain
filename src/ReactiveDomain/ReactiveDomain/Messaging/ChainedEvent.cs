using System;
using System.Threading;
using ReactiveDomain.Messages;

namespace ReactiveDomain.Messaging
{
    public class ChainedEvent : DomainEvent, IChainedMessage
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

        public override int MsgTypeId => TypeId;

        public Guid PrincipleId { get; }
        public readonly ChainSource Source;

        protected ChainedEvent(IChainedMessage source) :
            base(source.CorrelationId, source.MsgId)
        {
            PrincipleId = source.PrincipleId;
            Source = source.GetMemento();
        }
        protected ChainedEvent(Guid correlationId, Guid sourceId, Guid principleId) :
            base(correlationId, sourceId)
        {
            PrincipleId = principleId;
        }
    }

   
}
