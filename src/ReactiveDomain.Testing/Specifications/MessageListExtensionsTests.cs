#nullable enable
using System;
using ReactiveDomain.Messaging;
using Xunit;
using Xunit.Sdk;

namespace ReactiveDomain.Testing;

public sealed class MessageListExtensionsTests : DispatcherSpecification {
    [Fact]
    public void CanAssertTheNextItemByCorrelationId() {
        var msg = MessageBuilder.New(() => new TestEvent());
        Dispatcher.Publish(msg);
        TestQueue.AssertNext<TestEvent>(msg.CorrelationId);
    }

    [Fact]
    public void AssertNextThrowsWhenCorrelationIdDoesNotMatch() {
        var msg = MessageBuilder.New(() => new TestEvent());
        Dispatcher.Publish(msg);
        Assert.Throws<Exception>(() => TestQueue.AssertNext<TestEvent>(Guid.NewGuid()));
    }

    [Fact]
    public void AssertNextThrowsWhenTypeDoesNotMatch() {
        var msg = MessageBuilder.New(() => new TestEvent());
        Dispatcher.Publish(msg);
        Assert.Throws<Exception>(() => TestQueue.AssertNext<TestCommands.Command1>(msg.CorrelationId));
    }

    [Fact]
    public void AssertNextThrowsOnEmptyList() {
        Assert.Throws<InvalidOperationException>(() => TestQueue.AssertNext<TestEvent>(Guid.NewGuid()));
    }

    [Fact]
    public void CanAssertTheNextItemByCorrelationIdAndGetTheMessage() {
        var msg = MessageBuilder.New(() => new TestEvent());
        Dispatcher.Publish(msg);
        TestQueue.AssertNext<TestEvent>(msg.CorrelationId, out var m);
        Assert.Same(msg, m);
    }

    [Fact]
    public void AssertNextWithOutputThrowsWhenCorrelationIdDoesNotMatch() {
        var msg = MessageBuilder.New(() => new TestEvent());
        Dispatcher.Publish(msg);
        TestEvent? m = null;
        Assert.Throws<Exception>(() => TestQueue.AssertNext(Guid.NewGuid(), out m));
        Assert.Same(msg, m); // message is returned regardless of whether the IDs match
    }

    [Fact]
    public void AssertNextWithOutputThrowsWhenTypeDoesNotMatch() {
        var msg = MessageBuilder.New(() => new TestEvent());
        Dispatcher.Publish(msg);
        TestCommands.Command1? m = null;
        Assert.Throws<Exception>(() => TestQueue.AssertNext(msg.CorrelationId, out m));
        Assert.Null(m);
    }

    [Fact]
    public void AssertNextWithOutputThrowsOnEmptyList() {
        TestEvent? m = null;
        Assert.Throws<InvalidOperationException>(() => TestQueue.AssertNext(Guid.NewGuid(), out m));
        Assert.Null(m);
    }

    [Fact]
    public void CanAssertTheNextItemUsingACondition() {
        var msg = MessageBuilder.New(() => new PayloadEvent(1));
        Dispatcher.Publish(msg);
        TestQueue.AssertNext<PayloadEvent>(m => m.Payload == 1);
    }

    [Fact]
    public void AssertNextWithOutputThrowsWhenConditionIsFalse() {
        var msg = MessageBuilder.New(() => new TestEvent());
        Dispatcher.Publish(msg);
        TestEvent? m = null;
        Assert.Throws<Exception>(() => TestQueue.AssertNext(Guid.NewGuid(), out m));
        Assert.Same(msg, m); // message is returned regardless of whether the IDs match
    }

    [Fact]
    public void AssertNextWithConditionThrowsWhenTypeDoesNotMatch() {
        var msg = MessageBuilder.New(() => new PayloadEvent(1));
        Dispatcher.Publish(msg);
        Assert.Throws<TrueException>(() => TestQueue.AssertNext<PayloadEvent>(m => m.Payload == 0));
    }

    [Fact]
    public void AssertNextWithConditionThrowsOnEmptyList() {
        Assert.Throws<InvalidOperationException>(() => TestQueue.AssertNext<PayloadEvent>(m => m.Payload == 0));
    }

    [Fact]
    public void CanAssertEmptyOnAListOfMessages() {
        TestQueue.AssertEmpty();
    }

    [Fact]
    public void AssertEmptyThrowsWhenTheListIsNotEmpty() {
        var msg = MessageBuilder.New(() => new TestEvent());
        Dispatcher.Publish(msg);
        Assert.Throws<Exception>(() => TestQueue.AssertEmpty());
    }

    public record PayloadEvent(int Payload) : Event;
}