using System;

namespace ReactiveDomain.Messaging
{
    public class MessageBuilder
    {        
        private Guid? Correlation;
        private Guid? Causation;
        public static MessageBuilder From(ICorrelatedMessage message)
        {
            return new MessageBuilder
            {                 
                Correlation = message.CorrelationId,
                Causation = message.MsgId
            };
        }
        public T Build<T>(Func<T> MessageConstructor) where T : ICorrelatedMessage
        {
            var msg = MessageConstructor();
            msg.CorrelationId = Correlation ?? msg.MsgId;
            msg.CausationId = Causation ?? Guid.Empty;
            return msg;
        }
        public static T New<T>(Func<T> MessageConstructor) where T : ICorrelatedMessage
        {
            return new MessageBuilder().Build(MessageConstructor);
        }
    }    
}
