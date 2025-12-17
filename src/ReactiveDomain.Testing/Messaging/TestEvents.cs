using ReactiveDomain.Messaging;

// ReSharper disable MemberCanBeProtected.Global
// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Testing;

public record TestEvent : Event;
public record ParentTestEvent : Message;
public record ChildTestEvent : ParentTestEvent;
public record GrandChildTestEvent : ChildTestEvent;