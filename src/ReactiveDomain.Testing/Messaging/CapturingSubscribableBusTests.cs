using System;
using System.Reactive.Disposables;
using System.Reactive.Disposables.Fluent;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using Xunit;

namespace ReactiveDomain.Testing.Messaging;

public sealed class CapturingSubscribableBusTests : IDisposable {
    private readonly CapturingSubscribableBus _sut = new();

    [Fact]
    public void CanSubscribeToAll() {
        var handled = 0;
        _sut.SubscribeToAll(new AdHocHandler<IMessage>(_ => handled++));
        _sut.Publish(new TestEvent());
        _sut.Publish(new TestMessage());
        _sut.Publish(new TestCommands.Command1());
        Assert.Equal(3, handled);
    }

    [Fact]
    public void CanSubscribeToPublishedMessages() {
        var handled = 0;
        _sut.Subscribe(new AdHocHandler<TestEvent>(_ => handled++));
        _sut.Publish(new TestEvent());
        _sut.Publish(new TestMessage());
        Assert.Equal(2, _sut.PublishedMessages.Count);
        Assert.Equal(1, handled);
    }

    [Fact]
    public void PreventsMultipleSubscribersToTheSameCommand() {
        using var subscriber = new TestCommandSubscriber(_sut);
        Assert.Throws<ExistingHandlerException>(() => new TestCommandSubscriber(_sut));
    }

    [Fact]
    public void SubscribeReturnsCorrectDisposer() {
        var subscriber = new DisposingCommandSubscriber(_sut);
        _sut.Send(new TestCommands.Command2());
        subscriber.Dispose();
        Assert.Throws<CommandTimedOutException>(() => _sut.Send(new TestCommands.Command2()));
    }

    [Fact]
    public void CanUnsubscribeFromCommands() {
        var subscriber = new TransientCommandSubscriber(_sut);
        _sut.Send(new TestCommands.Command2());
        subscriber.Dispose();
        Assert.Throws<CommandTimedOutException>(() => _sut.Send(new TestCommands.Command2()));
    }

    [Fact]
    public void CanCaptureMessageInPublish() {
        var msg = MessageBuilder.New(() => new TestEvent());
        _sut.Publish(msg);
        var m = Assert.Single(_sut.PublishedMessages);
        Assert.Same(msg, m);
        Assert.Empty(_sut.SentCommands);
    }

    [Fact]
    public void CanCaptureCommandInSend() {
        using var subscriber = new TestCommandSubscriber(_sut);
        var cmd = new TestCommands.Command2();
        _sut.Send(cmd);
        var c = Assert.Single(_sut.SentCommands);
        Assert.Same(cmd, c);
        Assert.Empty(_sut.PublishedMessages);
    }

    [Fact]
    public void FailedCommandIsCapturedOnSend() {
        using var subscriber = new TransientCommandSubscriber(_sut) { FailOnSend = true };
        var cmd = MessageBuilder.New(() => new TestCommands.Command2());
        AssertEx.CommandThrows<Exception>(() => _sut.Send(cmd));
        var c = Assert.Single(_sut.SentCommands);
        Assert.Same(cmd, c);
        Assert.Empty(_sut.PublishedMessages);
    }

    [Fact]
    public void SubscriberHandlesSentCommand() {
        using var subscriber = new TestCommandSubscriber(_sut);
        var cmd = new TestCommands.Command2();
        _sut.Send(cmd);
        Assert.Equal(1, subscriber.TestCommand2Handled);
    }

    [Fact]
    public void CanCaptureCommandInTrySend() {
        using var subscriber = new TestCommandSubscriber(_sut);
        var cmd = new TestCommands.Command2();
        _sut.TrySend(cmd, out var response);
        Assert.IsType<Success>(response);
        var c = Assert.Single(_sut.SentCommands);
        Assert.Same(cmd, c);
        Assert.Empty(_sut.PublishedMessages);
    }

    [Fact]
    public void TrySendCapturesCommandOnFailure() {
        using var subscriber = new TransientCommandSubscriber(_sut) { FailOnSend = true };
        var cmd = new TestCommands.Command2();
        _sut.TrySend(cmd, out _);
        var c = Assert.Single(_sut.SentCommands);
        Assert.Same(cmd, c);
        Assert.Empty(_sut.PublishedMessages);
    }

    [Fact]
    public void TrySendOutputsCorrectResponseOnFailure() {
        using var subscriber = new TransientCommandSubscriber(_sut) { FailOnSend = true };
        var cmd = new TestCommands.Command2();
        _sut.TrySend(cmd, out var response);
        Assert.IsType<Fail>(response);
    }

    [Fact]
    public void TrySendFailsWhenNoSubscribers() {
        Assert.False(_sut.TrySend(new TestCommands.Command2(), out var response));
        var fail = Assert.IsType<Fail>(response);
        Assert.IsType<CommandTimedOutException>(fail.Exception);
    }

    [Fact]
    public void CanCaptureCommandInTrySendAsync() {
        using var subscriber = new TestCommandSubscriber(_sut);
        var cmd = new TestCommands.Command2();
        Assert.True(_sut.TrySendAsync(cmd));
        var c = Assert.Single(_sut.SentCommands);
        Assert.Same(cmd, c);
        Assert.Empty(_sut.PublishedMessages);
    }

    [Fact]
    public void TrySendAsyncCapturesCommandOnFailure() {
        using var subscriber = new TransientCommandSubscriber(_sut) { FailOnSend = true };
        var cmd = new TestCommands.Command2();
        _sut.TrySendAsync(cmd);
        var c = Assert.Single(_sut.SentCommands);
        Assert.Same(cmd, c);
        Assert.Empty(_sut.PublishedMessages);
    }

    [Fact]
    public void TrySendAsyncReturnsCorrectResponseOnFailure() {
        using var subscriber = new TransientCommandSubscriber(_sut) { FailOnSend = true };
        var cmd = new TestCommands.Command2();
        Assert.False(_sut.TrySendAsync(cmd));
    }

    [Fact]
    public void TrySendAsyncFailsWhenNoSubscribers() {
        Assert.False(_sut.TrySendAsync(new TestCommands.Command2()));
    }

    [Fact]
    public void CapturesAllMessagesInOrder() {
        using var subscriber = new TestCommandSubscriber(_sut);
        _sut.Send(new TestCommands.Command3());
        _sut.Publish(new TestEvent());
        _sut.Publish(new TestMessage());
        _sut.Send(new TestCommands.Command2());
        Assert.Collection(_sut.PublishedMessages,
            m => Assert.IsType<TestEvent>(m),
            m => Assert.IsType<TestMessage>(m));
        Assert.Collection(_sut.SentCommands,
            c => Assert.IsType<TestCommands.Command3>(c),
            c => Assert.IsType<TestCommands.Command2>(c));
        Assert.Collection(_sut.AllMessages,
            c => Assert.IsType<TestCommands.Command3>(c),
            m => Assert.IsType<TestEvent>(m),
            m => Assert.IsType<TestMessage>(m),
            c => Assert.IsType<TestCommands.Command2>(c));
    }

    [Fact]
    public void CanClearAllMessageLists() {
        using var subscriber = new TestCommandSubscriber(_sut);
        _sut.Send(new TestCommands.Command3());
        _sut.Publish(new TestEvent());
        _sut.Publish(new TestMessage());
        _sut.Send(new TestCommands.Command2());
        _sut.ClearMessages();
        Assert.Empty(_sut.PublishedMessages);
        Assert.Empty(_sut.SentCommands);
        Assert.Empty(_sut.AllMessages);
    }

    private class DisposingCommandSubscriber : IHandleCommand<TestCommands.Command2>, IDisposable {
        private readonly CompositeDisposable _disposables = [];
        public DisposingCommandSubscriber(IDispatcher bus) {
            // ReSharper disable once RedundantTypeArgumentsOfMethod
            bus.Subscribe<TestCommands.Command2>(this).DisposeWith(_disposables);
        }

        public CommandResponse Handle(TestCommands.Command2 command) {
            return command.Succeed();
        }
        public void Dispose() {
            _disposables.Dispose();
        }
    }

    private class TransientCommandSubscriber : TransientSubscriber, IHandleCommand<TestCommands.Command2> {
        public bool FailOnSend { get; init; }
        public TransientCommandSubscriber(IDispatcher bus) : base(bus) {
            // ReSharper disable once RedundantTypeArgumentsOfMethod
            Subscribe<TestCommands.Command2>(this);
        }
        public CommandResponse Handle(TestCommands.Command2 command) {
            return FailOnSend ? throw new Exception("Command failed") : command.Succeed();
        }
    }

    public void Dispose() {
        _sut.Dispose();
    }
}
