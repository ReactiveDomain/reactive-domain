using ReactiveDomain.Messaging;
using System;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Testing;

public record TestMessage : Message;
public record TestMessage2 : Message;
public record TestMessage3 : Message;
public record ParentTestMessage : Message;
public record ChildTestMessage : ParentTestMessage;
public record GrandChildTestMessage : ChildTestMessage;
public record CountedTestMessage(int MessageNumber) : Message;

public record CountedEvent(int MessageNumber) : Message, ICorrelatedMessage {
    public Guid CorrelationId { get; set; }
    public Guid CausationId { get; set; }
}