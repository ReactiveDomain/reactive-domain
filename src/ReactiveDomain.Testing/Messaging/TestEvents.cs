using System;
using System.Threading;

// ReSharper disable MemberCanBeProtected.Global
namespace ReactiveDomain.Messaging.Testing
{

    public class TestEvent : Event
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId => TypeId;

        public TestEvent(Guid correlationId, Guid sourceId) : base(correlationId, sourceId) { }
    }
    public class ParentTestEvent : Event
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId { get { return TypeId; } }
        public ParentTestEvent(Guid correlationId, Guid sourceId) : base(correlationId, sourceId) { }
    }
    public class ChildTestEvent : ParentTestEvent
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId { get { return TypeId; } }
        public ChildTestEvent(Guid correlationId, Guid sourceId) : base(correlationId, sourceId) { }
    }
    public class GrandChildTestEvent : ChildTestEvent
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId { get { return TypeId; } }
        public GrandChildTestEvent(Guid correlationId, Guid sourceId) : base(correlationId, sourceId) { }
    }

   
}

