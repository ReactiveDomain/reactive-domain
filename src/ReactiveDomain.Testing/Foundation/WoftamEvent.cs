using System;
using System.Threading;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Messages;

namespace ReactiveDomain.Testing
{
    public class WoftamEvent: Message, ICorrelatedMessage
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId => TypeId;
        public WoftamEvent(string property1, string property2)
        {
            Property1 = property1;
            Property2 = property2;
        }

        public string Property1 { get; private set; }
        public string Property2 { get; private set; }

        #region Implementation of ICorrelatedMessage
        public SourceId SourceId => SourceId.NullSourceId();
        public CorrelationId CorrelationId => Messaging.CorrelationId.NewId();
        #endregion
    }
}