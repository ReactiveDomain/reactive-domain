using ReactiveDomain.Messaging;
using System;

// ReSharper disable MemberCanBeProtected.Global
// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Testing {

    public class TestMessage : Message {}
    public class TestMessage2 : Message {}
    public class TestMessage3 : Message {}
    public class ParentTestMessage : Message {}
    public class ChildTestMessage : ParentTestMessage {}
    public class GrandChildTestMessage : ChildTestMessage {}
    public class CountedTestMessage : Message
    {
        public int MessageNumber;
        public CountedTestMessage(int msgNumber) {
            MessageNumber = msgNumber;
        }
    }

    public class CountedEvent : Message, ICorrelatedMessage {
        public Guid CorrelationId { get; set; }
        public Guid CausationId { get; set; }
        public int MessageNumber;
        public CountedEvent(int msgNumber) {
            MessageNumber = msgNumber;
        }
    }
}