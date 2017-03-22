using System;
using System.Threading;
using ReactiveDomain.Messaging;

// ReSharper disable MemberCanBeProtected.Global
namespace ReactiveDomain.Tests.Helpers
{

    public class TestMessage : Message
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId { get { return TypeId; } }

        public TestMessage(){}
    }
    public class TestMessage2 : Message
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId { get { return TypeId; } }
        public TestMessage2(){}
    }
    public class TestMessage3 : Message
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId { get { return TypeId; } }
        public TestMessage3(){}
    }

    public class ParentTestMessage : Message
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId { get { return TypeId; } }
        public ParentTestMessage(){}
    }
    public class ChildTestMessage : ParentTestMessage
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId { get { return TypeId; } }
        public ChildTestMessage(){}
    }
    public class GrandChildTestMessage : ChildTestMessage
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId { get { return TypeId; } }
        public GrandChildTestMessage(){}
    }

    public class CountedTestMessage : Message
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId { get { return TypeId; } }
        public int MessageNumber;

        public CountedTestMessage(int msgNumber)
        {
            MessageNumber = msgNumber;
        }
    }

    public class CountedEvent : DomainEvent
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId { get { return TypeId; } }
        public int MessageNumber;

        public CountedEvent(int msgNumber,
                Guid correlationId,
                Guid sourceId)
                : base(correlationId, sourceId)
        {
            MessageNumber = msgNumber;
        }
    }
}

