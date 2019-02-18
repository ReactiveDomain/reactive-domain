using System;
using ReactiveDomain.Messaging;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable NotAccessedField.Global
namespace ReactiveDomain.Testing
{
    public class TestAggregateMessages
    {
        public class NewAggregate : Event
        {
            public readonly Guid AggregateId;
            public NewAggregate(Guid aggregateId):base(CorrelationId.NewId(),SourceId.NullSourceId())
            {
                AggregateId = aggregateId;
            }
        }
        public class Increment : Event
        {
            public readonly Guid AggregateId;
            public readonly uint Amount;
            public Increment(Guid aggregateId, uint amount):base(CorrelationId.NewId(),SourceId.NullSourceId())
            {
                AggregateId = aggregateId;
                Amount = amount;
            }
        }

    }
}
