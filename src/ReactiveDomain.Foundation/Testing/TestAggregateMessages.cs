using System;
using System.Threading;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Messages;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable NotAccessedField.Global
namespace ReactiveDomain.Foundation.Testing
{
    public class TestAggregateMessages
    {
        public class NewAggregate : Message, ICorrelatedMessage
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public readonly Guid AggregateId;
            public NewAggregate(Guid aggregateId)
            {
                AggregateId = aggregateId;
            }
            #region Implementation of ICorrelatedMessage
            public Guid? SourceId => null;
            public Guid CorrelationId => AggregateId;
            #endregion
        }
        public class Increment : Message, ICorrelatedMessage
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public readonly Guid AggregateId;
            public readonly uint Amount;
            public Increment(Guid aggregateId, uint amount)
            {
                AggregateId = aggregateId;
                Amount = amount;
            }

            #region Implementation of ICorrelatedMessage
            public Guid? SourceId => null;
            public Guid CorrelationId => AggregateId;
            #endregion
        }

    }
}
