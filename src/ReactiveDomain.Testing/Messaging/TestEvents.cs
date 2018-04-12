using System;
using System.Threading;
using Newtonsoft.Json;
using ReactiveDomain.Messaging.Messages;

// ReSharper disable MemberCanBeProtected.Global
namespace ReactiveDomain.Messaging.Testing
{

    public class TestEvent : Event
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId => TypeId;
        public TestEvent(CorrelatedMessage source) : base(source) { }
        [JsonConstructor]
        public TestEvent(CorrelationId correlationId, SourceId sourceId):base(correlationId, sourceId) { }
    }
    public class ParentTestEvent : Event
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId { get { return TypeId; } }
        public ParentTestEvent(CorrelatedMessage source) : base(source) { }
        [JsonConstructor]
        public ParentTestEvent(CorrelationId correlationId, SourceId sourceId):base(correlationId, sourceId) { }
    }
    public class ChildTestEvent : ParentTestEvent
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId { get { return TypeId; } }
        
        public ChildTestEvent(CorrelatedMessage source) : base(source) { }
        [JsonConstructor]
        public ChildTestEvent(CorrelationId correlationId, SourceId sourceId):base(correlationId, sourceId) { }
    }
    public class GrandChildTestEvent : ChildTestEvent
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId { get { return TypeId; } }
        public GrandChildTestEvent(CorrelatedMessage source) : base(source) { }
        [JsonConstructor]
        public GrandChildTestEvent(CorrelationId correlationId, SourceId sourceId):base(correlationId, sourceId) { }
    }

   
}

