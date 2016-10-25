using System;
using System.ComponentModel;
using System.Threading;
using ReactiveDomain.Messages;

namespace ReactiveDomain.Messaging
{
    public class DomainEvent : Event, ICorrelatedMessage
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

        public override int MsgTypeId => TypeId;

        public Guid CorrelationId { get; }
     
        public Guid? SourceId { get; }

        protected DomainEvent(Guid correlationId, Guid sourceId)
        {
            CorrelationId = correlationId;
            SourceId = sourceId;
        }
    }
}
