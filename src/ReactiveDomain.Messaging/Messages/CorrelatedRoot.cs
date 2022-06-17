using System;

namespace ReactiveDomain.Messaging.Messages
{
    public class CorrelatedRoot : Event
    {
        public CorrelatedRoot(Guid? correlationId = null)
        {
            CorrelationId = correlationId ?? Guid.NewGuid();
        }
    }
}
