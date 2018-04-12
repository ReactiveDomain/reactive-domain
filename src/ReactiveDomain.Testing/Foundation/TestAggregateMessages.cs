using System;
using System.Threading;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Messages;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable NotAccessedField.Global
namespace ReactiveDomain.Testing
{
    public class TestAggregateMessages
    {
        public class NewAggregate : CorrelatedMessage
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public readonly Guid AggregateId;
            public NewAggregate(Guid aggregateId):base(CorrelationId.NewId(),SourceId.NullSourceId())
            {
                AggregateId = aggregateId;
            }
        }
        public class Increment : CorrelatedMessage
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
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
