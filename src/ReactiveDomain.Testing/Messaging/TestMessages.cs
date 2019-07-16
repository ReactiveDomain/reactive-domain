using ReactiveDomain.Messaging;
using System;

// ReSharper disable MemberCanBeProtected.Global
// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Testing {

    public class TestMessage : IMessage {
        public Guid MsgId { get; private set; }
        public TestMessage()
        {
            MsgId = Guid.NewGuid();
        }
    }
    public class TestMessage2 : IMessage {
        public Guid MsgId { get; private set; }
        public TestMessage2()
        {
            MsgId = Guid.NewGuid();
        }
    }
    public class TestMessage3 : IMessage {
        public Guid MsgId { get; private set; }
        public TestMessage3()
        {
            MsgId = Guid.NewGuid();
        }
    }
    public class ParentTestMessage : IMessage {
        public Guid MsgId { get; private set; }
        public ParentTestMessage()
        {
            MsgId = Guid.NewGuid();
        }
    }
    public class ChildTestMessage : ParentTestMessage {}
    public class GrandChildTestMessage : ChildTestMessage {}
    public class CountedTestMessage : IMessage
    {
        public Guid MsgId { get; private set; }
        public int MessageNumber;
        public CountedTestMessage(int msgNumber) {
            MsgId = Guid.NewGuid();
            MessageNumber = msgNumber;
        }
    }

    public class CountedEvent : ICorrelatedMessage {
        public Guid MsgId { get; private set; }
        public Guid CorrelationId { get; set; }
        public Guid CausationId { get; set; }
        public int MessageNumber;
        public CountedEvent(int msgNumber) {
            MsgId = Guid.NewGuid();
            MessageNumber = msgNumber;
        }
    }
}