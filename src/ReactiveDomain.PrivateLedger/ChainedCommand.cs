using System;
using System.Threading;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Messages;

namespace ReactiveDomain.PrivateLedger
{
    public class ChainedCommand : Command, IChainedMessage
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId => TypeId;
        public Guid PrincipalId { get; }
        public readonly ChainSource Source;
        public ChainedCommand(IChainedMessage source) :
            base(source.CorrelationId, source.SourceId, null)
        {
            PrincipalId = source.PrincipalId;
            Source = source.GetMemento();
        }

        public ChainedCommand(CorrelatedMessage source, Guid principalId) :
            base(source.CorrelationId, source.SourceId, null)
        {
            PrincipalId = principalId;
        }

    }
}