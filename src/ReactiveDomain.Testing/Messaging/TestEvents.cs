using Newtonsoft.Json;
using ReactiveDomain.Messaging;

// ReSharper disable MemberCanBeProtected.Global
// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Testing
{
    public class TestEvent : Event
    {
   
        public TestEvent(CorrelatedMessage source) : base(source) { }
        [JsonConstructor]
        public TestEvent(CorrelationId correlationId, SourceId sourceId):base(correlationId, sourceId) { }
    }
    public class ParentTestEvent : Event
    {
      
        public ParentTestEvent(CorrelatedMessage source) : base(source) { }
        [JsonConstructor]
        public ParentTestEvent(CorrelationId correlationId, SourceId sourceId):base(correlationId, sourceId) { }
    }
    public class ChildTestEvent : ParentTestEvent
    {
     
        
        public ChildTestEvent(CorrelatedMessage source) : base(source) { }
        [JsonConstructor]
        public ChildTestEvent(CorrelationId correlationId, SourceId sourceId):base(correlationId, sourceId) { }
    }
    public class GrandChildTestEvent : ChildTestEvent
    {
     
        public GrandChildTestEvent(CorrelatedMessage source) : base(source) { }
        [JsonConstructor]
        public GrandChildTestEvent(CorrelationId correlationId, SourceId sourceId):base(correlationId, sourceId) { }
    }

   
}

