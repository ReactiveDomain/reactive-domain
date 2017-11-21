using System;
using System.Threading;
using System.Threading.Tasks;
using ReactiveDomain.Bus;
using ReactiveDomain.Messaging;
using ReactiveDomain.Tests.Specifications;
using Xunit;

namespace ReactiveDomain.Tests
{

    // ReSharper disable once InconsistentNaming
    public class precanceled_commands_via_cancellation_token : CommandBusSpecification
    {
        protected override void Given()
        {
            Bus.Subscribe(new TokenCancellableCmdHandler());
        }

        protected CancellationTokenSource TokenSource;

        protected override void When()
        {
            TokenSource = new CancellationTokenSource();
            TokenSource.Cancel();
            Task.Run(
                () =>
                    Bus.Fire(new TestTokenCancellableCmd(Guid.NewGuid(), Guid.Empty, TokenSource.Token),
                        responseTimeout: TimeSpan.FromSeconds(2)));
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
    public class can_cancel_commands_via_cancellation_token : CommandBusSpecification
    {
        protected override void Given()
        {
            Bus.Subscribe(new TokenCancellableCmdHandler());
        }

        protected CancellationTokenSource TokenSource;
        protected override void When()
        {
            TokenSource = new CancellationTokenSource();
            Task.Run(() => Bus.Fire(new TestTokenCancellableCmd(Guid.NewGuid(), Guid.Empty, TokenSource.Token), responseTimeout: TimeSpan.FromSeconds(2)));
        }
        [Fact]
        public void will_succeed_if_not_canceled()
        {
            TestQueue.WaitFor<TestTokenCancellableCmd>(TimeSpan.FromSeconds(2));
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
    public class can_cancel_nested_commands_via_cancellation_token : CommandBusSpecification
    {
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
            Task.Run(
                () =>
                    Bus.Fire(new TestTokenCancellableCmd(Guid.NewGuid(), Guid.Empty, TokenSource.Token),
                        responseTimeout: TimeSpan.FromSeconds(2)));
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
        public CommandResponse Handle(TestTokenCancellableCmd command)
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

        public TestTokenCancellableCmd(
            Guid correlationId,
            Guid? sourceId,
            CancellationToken cancellationToken) :
            base(correlationId, sourceId, cancellationToken)
        {
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
