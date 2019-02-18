using System;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Testing
{
    public class TestWoftamAggregateCreated: Event
    {
      
        public TestWoftamAggregateCreated(Guid aggregateId):base(CorrelationId.NewId(), SourceId.NullSourceId())
        {
            AggregateId = aggregateId;
        }

        public Guid AggregateId { get; private set; }

    }
}