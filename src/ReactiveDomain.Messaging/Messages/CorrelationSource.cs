

using System;

namespace ReactiveDomain.Messaging.Messages
{
    public class CorrelationSource : ICorrelatedMessage
    {
        public Guid MsgId{ get;}
        public Guid CausationId { get; set; }
        public Guid CorrelationId {  get; set; }
        public CorrelationSource(Guid id, Guid causationId,Guid correlationId)
        {
            MsgId = id;
            CausationId = causationId;  
            CorrelationId = correlationId;
        }
        
    }
}
