#nullable enable
using System;
using System.Threading;
using System.Threading.Tasks;
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

    [Fact]
    public void CanWaitForAMessageOnAList() {
        Task.Run(() => {
            Thread.Sleep(20);
            var msg = MessageBuilder.New(() => new TestEvent());
            Dispatcher.Publish(msg);
        });
        TestQueue.WaitFor<TestEvent>(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void WaitForTimesOutWhenMessageDoesNotArriveInTime() {
        Task.Run(() => {
            Thread.Sleep(20);
            var msg = MessageBuilder.New(() => new TestEvent());
            Dispatcher.Publish(msg);
        });
        Assert.Throws<TimeoutException>(() => TestQueue.WaitFor<TestEvent>(TimeSpan.FromMilliseconds(10)));
    }

    [Fact]
    public void CanWaitForMultipleMessagesOnAList() {
        Task.Run(() => {
            Thread.Sleep(20);
            var msg = MessageBuilder.New(() => new TestEvent());
            Dispatcher.Publish(msg);
            msg = MessageBuilder.New(() => new TestEvent());
            Dispatcher.Publish(msg);
        });
        TestQueue.WaitForMultiple<TestEvent>(2, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void WaitForTimesOutWhenMultipleMessagesDoNotArriveInTime() {
        Task.Run(() => {
            Thread.Sleep(20);
            var msg = MessageBuilder.New(() => new TestEvent());
            Dispatcher.Publish(msg);
            msg = MessageBuilder.New(() => new TestEvent());
            Dispatcher.Publish(msg);
        });
        Assert.Throws<TimeoutException>(() => TestQueue.WaitForMultiple<TestEvent>(2, TimeSpan.FromMilliseconds(10)));
    }

    public record PayloadEvent(int Payload) : Event;
}