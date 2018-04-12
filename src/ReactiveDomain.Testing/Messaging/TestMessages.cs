using System;
using System.Threading;
using ReactiveDomain.Messaging.Messages;

// ReSharper disable MemberCanBeProtected.Global
namespace ReactiveDomain.Messaging.Testing {

    public class TestMessage : Message {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId => TypeId;

        public TestMessage() { }
    }
    public class TestMessage2 : Message {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId => TypeId;
        public TestMessage2() { }
    }
    public class TestMessage3 : Message {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId => TypeId;
        public TestMessage3() { }
    }

    public class ParentTestMessage : Message {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId => TypeId;
        public ParentTestMessage() { }
    }
    public class ChildTestMessage : ParentTestMessage {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId => TypeId;
        public ChildTestMessage() { }
    }
    public class GrandChildTestMessage : ChildTestMessage {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId => TypeId;
        public GrandChildTestMessage() { }
    }

    public class CountedTestMessage : Message {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId => TypeId;
        public int MessageNumber;

        public CountedTestMessage(int msgNumber) {
            MessageNumber = msgNumber;
        }
    }

    public class CountedEvent : Event {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId => TypeId;
        public int MessageNumber;

        public CountedEvent(int msgNumber,
                CorrelatedMessage source)
                : base(source) {
            MessageNumber = msgNumber;
        }
    }

}

