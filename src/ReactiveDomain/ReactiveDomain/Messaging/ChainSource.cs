using System;
using Newtonsoft.Json;
using ReactiveDomain.Messages;

namespace ReactiveDomain.Messaging
{
    public static class ChainSourcehelpers
    {
        public static ChainSource GetMemento(this IChainedMessage source)
        {
            return new ChainSource(source);
        }
    }
    public class ChainSource : IChainedMessage
    {
        public Guid MsgId { get; }
        public Guid? SourceId { get; }
        public Guid CorrelationId { get; }
        public Guid PrincipleId { get; }

        public ChainSource(IChainedMessage source)
            : this(source.MsgId, source.SourceId, source.CorrelationId, source.PrincipleId)
        {
        }
        [JsonConstructor]
        public ChainSource(
            Guid msgId,
            Guid? sourceId,
            Guid correlationId,
            Guid principleId)
        {
            MsgId = msgId;
            SourceId = sourceId;
            CorrelationId = correlationId;
            PrincipleId = principleId;
        }

    }
}