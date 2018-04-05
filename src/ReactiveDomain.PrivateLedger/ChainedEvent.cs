﻿using System;
using System.Threading;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Messages;

namespace ReactiveDomain.PrivateLedger
{
    public class ChainedEvent : Event, IChainedMessage
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

        public override int MsgTypeId => TypeId;

        public Guid PrincipalId { get; }
        public readonly ChainSource Source;

        protected ChainedEvent(IChainedMessage source) :
            base(source.CorrelationId, new SourceId(source))
        {
            PrincipalId = source.PrincipalId;
            Source = source.GetMemento();
        }
        protected ChainedEvent(CorrelationId correlationId, SourceId sourceId, Guid principalId) :
            base(correlationId, sourceId)
        {
            PrincipalId = principalId;
        }
    }

   
}
