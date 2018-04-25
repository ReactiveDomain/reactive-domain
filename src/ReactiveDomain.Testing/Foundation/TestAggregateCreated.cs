using System;
using System.Threading;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Messages;

namespace ReactiveDomain.Testing
{
    public class TestWoftamAggregateCreated: CorrelatedMessage
    {
      
        public TestWoftamAggregateCreated(Guid aggregateId):base(CorrelationId.NewId(), SourceId.NullSourceId())
        {
            AggregateId = aggregateId;
        }

        public Guid AggregateId { get; private set; }

    }
}