using System;

namespace ReactiveDomain.Messaging
{
    public abstract class Event : Message, ICorrelatedMessage, IEvent
    {
        protected ushort Version = 1;
        public Guid CorrelationId { get; set; }
        public Guid CausationId { get; set; }
    }
}
