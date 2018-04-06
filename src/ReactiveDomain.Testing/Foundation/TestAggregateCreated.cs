using System;
using System.Threading;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Messages;

namespace ReactiveDomain.Testing
{
    public class TestWoftamAggregateCreated: Message, ICorrelatedMessage
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId => TypeId;
        public TestWoftamAggregateCreated(Guid aggregateId)
        {
            AggregateId = aggregateId;
            CorrelationId = CorrelationId.NewId();
        }

        public Guid AggregateId { get; private set; }

        #region Implementation of ICorrelatedMessage

        public SourceId SourceId => SourceId.NullSourceId();
        public CorrelationId CorrelationId { get; }

        #endregion
    }
}