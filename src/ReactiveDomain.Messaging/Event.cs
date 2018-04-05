using System;
using System.Threading;
using ReactiveDomain.Messaging.Messages;

namespace ReactiveDomain.Messaging
{
    public class Event : Message, IEvent, ICorrelatedMessage
    {
        protected ushort Version = 1;

        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

        public override int MsgTypeId => TypeId;

        public Guid CorrelationId { get; }
     
        public Guid? SourceId { get; }

        protected Event(Guid correlationId, Guid sourceId)
        {
            CorrelationId = correlationId;
            SourceId = sourceId;
        }
    }
}
