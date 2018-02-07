using System;
using System.Threading;

// ReSharper disable MemberCanBeProtected.Global
namespace ReactiveDomain.Messaging.Testing
{

    public class TestDomainEvent : DomainEvent
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId => TypeId;

        public TestDomainEvent(Guid correlationId, Guid sourceId) : base(correlationId, sourceId) { }
    }
    public class ParentTestDomainEvent : DomainEvent
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId { get { return TypeId; } }
        public ParentTestDomainEvent(Guid correlationId, Guid sourceId) : base(correlationId, sourceId) { }
    }
    public class ChildTestDomainEvent : ParentTestDomainEvent
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId { get { return TypeId; } }
        public ChildTestDomainEvent(Guid correlationId, Guid sourceId) : base(correlationId, sourceId) { }
    }
    public class GrandChildTestDomainEvent : ChildTestDomainEvent
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId { get { return TypeId; } }
        public GrandChildTestDomainEvent(Guid correlationId, Guid sourceId) : base(correlationId, sourceId) { }
    }

   
}

