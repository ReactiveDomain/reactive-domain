using System;
using System.Threading;
using ReactiveDomain.Messages;

namespace ReactiveDomain.Messaging
{
    public class ChainedCommand : Command, IChainedMessage
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId => TypeId;
        public Guid PrincipleId { get; }
        public readonly ChainSource Source;
        public ChainedCommand(IChainedMessage source) :
            base(source.CorrelationId, source.MsgId)
        {
            PrincipleId = source.PrincipleId;
            Source = source.GetMemento();
        }

        public ChainedCommand(Guid correlationId, Guid? sourceId, Guid principleId) :
            base(correlationId, sourceId)
        {
            PrincipleId = principleId;
        }

    }
}