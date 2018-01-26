using System;
using System.Threading;
using ReactiveDomain.Messaging;
using ReactiveDomain.Tests.Specifications;
using Xunit;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Tests
{

    // ReSharper disable once InconsistentNaming
    public class precanceled_commands_via_cancellation_token : CommandQueueSpecification
    {
        public precanceled_commands_via_cancellation_token() : base(1, 2500, 2500) { }
        protected override void Given()
        {
            Bus.Subscribe(new TokenCancellableCmdHandler());
        }

        protected CancellationTokenSource TokenSource;

        protected override void When()
        {
            TokenSource = new CancellationTokenSource();
            TokenSource.Cancel();
            Queue.Handle(new TestTokenCancellableCmd(false, Guid.NewGuid(), Guid.Empty, TokenSource.Token));
        }
        [Fact]
        public void canceled_commands_will_not_fire()
        {
            TestQueue.WaitFor<Canceled>(TimeSpan.FromSeconds(2));
            TestQueue.Commands
                        .AssertEmpty();
            TestQueue.Responses
                        .AssertNext<Canceled>(_ => true)
                        .AssertEmpty();
        }
    }

    // ReSharper disable once InconsistentNaming
    public class can_cancel_concurrent_commands : CommandQueueSpecification
    {
        public can_cancel_concurrent_commands() : base(2, 2500, 2500)
        {

        }
        protected override void Given()
        {
            Handler = new TokenCancellableCmdHandler();
            Bus.Subscribe(Handler);
        }
        protected TokenCancellableCmdHandler Handler;
        protected CancellationTokenSource TokenSource1;
        protected CancellationTokenSource TokenSource2;
        protected TestTokenCancellableCmd Cmd1;
        protected TestTokenCancellableCmd Cmd2;
        protected override void When()
        {
            TokenSource1 = new CancellationTokenSource();
            TokenSource2 = new CancellationTokenSource();
            Cmd1 = new TestTokenCancellableCmd(false, Guid.NewGuid(), Guid.Empty, TokenSource1.Token);
            Cmd2 = new TestTokenCancellableCmd(false, Guid.NewGuid(), Guid.Empty, TokenSource2.Token);

            Queue.Handle(Cmd1);
            Queue.Handle(Cmd2);
        }

        [Fact]
        public void will_not_cross_cancel()
        {

            TestQueue.WaitFor<TestTokenCancellableCmd>(TimeSpan.FromSeconds(2));
            TokenSource1.Cancel();
            Handler.ParkedMessages[Cmd1.MsgId].Set();
            TestQueue.WaitFor<Canceled>(TimeSpan.FromSeconds(2));
            Handler.ParkedMessages[Cmd2.MsgId].Set();
            TestQueue.WaitFor<Success>(TimeSpan.FromSeconds(3));
            TestQueue.Commands
                        .AssertNext<TestTokenCancellableCmd>(_ => true)
                        .AssertNext<TestTokenCancellableCmd>(_ => true)
                        .AssertEmpty();
            TestQueue.Responses
                        .AssertNext<Canceled>(msg => msg.SourceCommand.MsgId == Cmd1.MsgId)
                        .AssertNext<Success>(msg => msg.SourceCommand.MsgId == Cmd2.MsgId)
                        .AssertEmpty();
        }

    }

    // ReSharper disable once InconsistentNaming
    public class can_cancel_commands_via_cancellation_token : CommandQueueSpecification
    {
        public can_cancel_commands_via_cancellation_token() : base(1, 2500, 2500)
        {

        }
        private TokenCancellableCmdHandler _handler;
        private TestTokenCancellableCmd _cmd;
        protected override void Given()
        {
            _handler = new TokenCancellableCmdHandler();
            Bus.Subscribe(_handler);
        }

        protected CancellationTokenSource TokenSource;
        protected override void When()
        {
            TokenSource = new CancellationTokenSource();
            _cmd = new TestTokenCancellableCmd(false, Guid.NewGuid(), Guid.Empty, TokenSource.Token);
            Queue.Handle(_cmd);
        }
        [Fact]
        public void will_succeed_if_not_canceled()
        {
            TestQueue.WaitFor<TestTokenCancellableCmd>(TimeSpan.FromSeconds(2));
            _handler?.ParkedMessages[_cmd.MsgId].Set();
            TestQueue.WaitFor<Success>(TimeSpan.FromSeconds(2));
            TestQueue.Commands
                        .AssertNext<TestTokenCancellableCmd>(_ => true)
                        .AssertEmpty();
            TestQueue.Responses
                        .AssertNext<Success>(_ => true)
                        .AssertEmpty();
        }
        [Fact]
        public void can_cancel_immediately()
        {
            TestQueue.WaitFor<TestTokenCancellableCmd>(TimeSpan.FromSeconds(2));
            TokenSource.Cancel();
            TestQueue.WaitFor<Canceled>(TimeSpan.FromSeconds(2));
            TestQueue.Commands
                        .AssertNext<TestTokenCancellableCmd>(_ => true)
                        .AssertEmpty();
            TestQueue.Responses
                        .AssertNext<Canceled>(_ => true)
                        .AssertEmpty();
        }
        [Fact]
        public void can_cancel_after_delay()
        {
            TestQueue.WaitFor<TestTokenCancellableCmd>(TimeSpan.FromSeconds(2));
            Thread.Sleep(500);
            TokenSource.Cancel();
            TestQueue.WaitFor<Canceled>(TimeSpan.FromSeconds(2));
            TestQueue.Commands
                        .AssertNext<TestTokenCancellableCmd>(_ => true)
                        .AssertEmpty();
            TestQueue.Responses
                        .AssertNext<Canceled>(_ => true)
                        .AssertEmpty();

        }
    }

    // ReSharper disable once InconsistentNaming
    public class can_cancel_nested_commands_via_cancellation_token : CommandQueueSpecification
    {
        public can_cancel_nested_commands_via_cancellation_token() : base(1, 2500, 2500)
        {
        }
        protected override void Given()
        {
            var hndl = new NestedTokenCancellableCmdHandler(Bus);
            Bus.Subscribe<TestTokenCancellableCmd>(hndl);
            Bus.Subscribe<NestedTestTokenCancellableCmd>(hndl);
        }

        protected CancellationTokenSource TokenSource;

        protected override void When()
        {
            TokenSource = new CancellationTokenSource();
            Queue.Handle(new TestTokenCancellableCmd(false, Guid.NewGuid(), Guid.Empty, TokenSource.Token));
        }
        [Fact]
        public void can_cancel_nested_commands_immediately()
        {
            TestQueue.WaitFor<TestTokenCancellableCmd>(TimeSpan.FromSeconds(2));
            TokenSource.Cancel();
            TestQueue.WaitFor<Canceled>(TimeSpan.FromSeconds(2));
            TestQueue.Commands
                        .AssertNext<TestTokenCancellableCmd>(_ => true)
                        .AssertNext<NestedTestTokenCancellableCmd>(_ => true)
                        .AssertEmpty();
            TestQueue.Responses
                        .AssertNext<Canceled>(_ => true)
                        .AssertNext<Fail>(_ => true)
                        .AssertEmpty();
        }
        [Fact]
        public void can_cancel_nested_commands_after_delay()
        {
            TestQueue.WaitFor<TestTokenCancellableCmd>(TimeSpan.FromSeconds(2));
            Thread.Sleep(500);
            TokenSource.Cancel();
            TestQueue.WaitFor<Canceled>(TimeSpan.FromSeconds(2));
            TestQueue.Commands
                        .AssertNext<TestTokenCancellableCmd>(_ => true)
                        .AssertNext<NestedTestTokenCancellableCmd>(_ => true)
                        .AssertEmpty();
            TestQueue.Responses
                        .AssertNext<Canceled>(_ => true)
                        .AssertNext<Fail>(_ => true)
                        .AssertEmpty();
        }

    }

    public class TokenCancellableCmdHandler : IHandleCommand<TestTokenCancellableCmd>
    {
        private readonly TimeSpan _maxTimeout;
        public TokenCancellableCmdHandler(int maxTimeoutMs = 5000)
        {
            _maxTimeout = TimeSpan.FromMilliseconds(maxTimeoutMs);
        }
        public ReadOnlyDictionary<Guid, ManualResetEventSlim> ParkedMessages => new ReadOnlyDictionary<Guid, ManualResetEventSlim>(_parkedMessages);
        private Dictionary<Guid, ManualResetEventSlim> _parkedMessages = new Dictionary<Guid, ManualResetEventSlim>();
        public CommandResponse Handle(TestTokenCancellableCmd command)
        {
            var start = DateTime.Now;
            var release = new ManualResetEventSlim();
            _parkedMessages.Add(command.MsgId, release);

            while ((DateTime.Now - start) < _maxTimeout)
            {
                release.Wait(10);
                if (command.IsCanceled)
                    return command.Canceled();
                if (release.IsSet)
                    if (command.RequestFail)
                        return command.Fail();
                    else
                        return command.Succeed();
            }
            return command.Fail(new TimeoutException());
        }
    }

    public class NestedTokenCancellableCmdHandler :
        IHandleCommand<TestTokenCancellableCmd>,
        IHandleCommand<NestedTestTokenCancellableCmd>
    {
        private readonly IGeneralBus _bus;

        public NestedTokenCancellableCmdHandler(IGeneralBus bus)
        {
            _bus = bus;
        }

        public CommandResponse Handle(TestTokenCancellableCmd command)
        {
            _bus.Fire(new NestedTestTokenCancellableCmd(Guid.NewGuid(), command.MsgId, command.CancellationToken));
            if (command.IsCanceled)
                return command.Canceled();
            else
                return command.Succeed();
        }

        public CommandResponse Handle(NestedTestTokenCancellableCmd command)
        {
            for (int i = 0; i < 5; i++)
            {
                if (command.IsCanceled)
                    return command.Canceled();
                Thread.Sleep(200);
            }
            return command.Succeed();
        }

    }

    public class TestTokenCancellableCmd : TokenCancellableCommand
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId => TypeId;

        public readonly bool RequestFail;
        public TestTokenCancellableCmd(
            bool requestFail,
            Guid correlationId,
            Guid? sourceId,
            CancellationToken cancellationToken) :
            base(correlationId, sourceId, cancellationToken)
        {
            RequestFail = requestFail;
        }

    }
    public class NestedTestTokenCancellableCmd : TokenCancellableCommand
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId => TypeId;

        public NestedTestTokenCancellableCmd(
            Guid correlationId,
            Guid? sourceId,
            CancellationToken cancellationToken) :
            base(correlationId, sourceId, cancellationToken)
        {
        }

    }
}
