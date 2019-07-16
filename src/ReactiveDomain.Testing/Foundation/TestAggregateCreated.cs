using System;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Testing
{
    public class TestWoftamAggregateCreated : IEvent
    {
        public Guid MsgId { get; private set; }
        public Guid AggregateId { get; private set; }
        public TestWoftamAggregateCreated(Guid aggregateId)
        {
            MsgId = Guid.NewGuid();
            AggregateId = aggregateId;
        }
    }
}