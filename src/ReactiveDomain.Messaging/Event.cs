using System;
using System.Diagnostics.Tracing;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using ReactiveDomain.Messaging.Messages;

namespace ReactiveDomain.Messaging {
    public class Event : CorrelatedMessage, IEvent  {
        protected ushort Version = 1;
       
        protected Event(CorrelatedMessage source):base(source.CorrelationId,new SourceId(source)){}
        [JsonConstructor]
        protected Event(CorrelationId correlationId, SourceId sourceId):base(correlationId, sourceId) { }
    }
}
