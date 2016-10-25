using System;
using System.Threading;
using ReactiveDomain.Messages;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Tests.EventStore
{
    public class TestWoftamAggregateCreated: Message, ICorrelatedMessage
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId => TypeId;
        public TestWoftamAggregateCreated(Guid aggregateId)
        {
            AggregateId = aggregateId;
        }

        public Guid AggregateId { get; private set; }

        #region Implementation of ICorrelatedMessage

        public Guid? SourceId => null;
        public Guid CorrelationId => AggregateId;

        #endregion
    }
}