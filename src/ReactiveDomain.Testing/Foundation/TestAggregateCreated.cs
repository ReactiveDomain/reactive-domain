using System;
using System.Threading;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Messages;

namespace ReactiveDomain.Testing
{
    public class TestWoftamAggregateCreated: CorrelatedMessage
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId => TypeId;
        public TestWoftamAggregateCreated(Guid aggregateId):base(CorrelationId.NewId(), SourceId.NullSourceId())
        {
            AggregateId = aggregateId;
        }

        public Guid AggregateId { get; private set; }

    }
}