using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Messaging.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests
{
    // ReSharper disable once InconsistentNaming
    public class precanceled_commands_via_cancellation_token : CommandQueueSpecification
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
            Queue.Handle(new TestTokenCancellableCmd(false, CorrelationId.NewId(), SourceId.NullSourceId(), TokenSource.Token));
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
    public class can_cancel_concurrent_commands :
        IHandleCommand<TestTokenCancellableCmd>
    {
        private CancellationTokenSource _tokenSource1;
        private CancellationTokenSource _tokenSource2;
        private TestTokenCancellableCmd _cmd1;
        private TestTokenCancellableCmd _cmd2;
        private CommandBus _bus = new CommandBus("test", 3, false);
        private long _releaseCmd;
        private long _gotCmd;
        private Guid _canceled;
        private Guid _succeeded;
        private long _completed;
        public can_cancel_concurrent_commands()
        {
            _bus.Subscribe(this);
        }

        [Fact]
        public void will_not_cross_cancel()
        {
            _tokenSource1 = new CancellationTokenSource();
            _tokenSource2 = new CancellationTokenSource();
            _cmd1 = new TestTokenCancellableCmd(false, CorrelationId.NewId(), SourceId.NullSourceId(), _tokenSource1.Token);
            _cmd2 = new TestTokenCancellableCmd(false, CorrelationId.NewId(), SourceId.NullSourceId(), _tokenSource2.Token);
            _bus.TryFire(_cmd1);
            _bus.TryFire(_cmd2);
            SpinWait.SpinUntil(() => Interlocked.Read(ref _gotCmd) == 1, 200);
            _tokenSource1.Cancel();
            Interlocked.Increment(ref _releaseCmd);
            SpinWait.SpinUntil(() => Interlocked.Read(ref _completed) == 2, 2000);
            Assert.True(_cmd1.MsgId == _canceled, "Canceled Command not canceled");
            Assert.True(_cmd2.MsgId == _succeeded, "Wrong Command canceled");

        }
        public CommandResponse Handle(TestTokenCancellableCmd cmd)
        {
            Interlocked.Increment(ref _gotCmd);
            SpinWait.SpinUntil(() => Interlocked.Read(ref _releaseCmd) == 1, 5000);


            if (cmd.IsCanceled)
            {
                _canceled = cmd.MsgId;
            }
            else
            {
                _succeeded = cmd.MsgId;
            }
            Interlocked.Increment(ref _completed);
            return cmd.IsCanceled ? cmd.Canceled() : cmd.Succeed();
        }
    }
    // ReSharper disable once InconsistentNaming
    public class can_cancel_commands_via_cancellation_token :
        IHandleCommand<TestTokenCancellableCmd>,
        IHandleCommand<TestTokenCancellableLongRunningCmd>
    {
        private readonly IGeneralBus _bus;
        private long _canceled;
        private long _success;
        private long _gotCmd;
        CancellationTokenSource _tokenSource;
        public can_cancel_commands_via_cancellation_token()
        {
            _bus = new CommandBus(
                nameof(can_cancel_nested_commands_via_cancellation_token),
                3,
                false,
                TimeSpan.FromSeconds(2.5),
                TimeSpan.FromSeconds(2.5));

        }
        [Fact]
        public void will_succeed_if_not_canceled()
        {

            _bus.Subscribe<TestTokenCancellableCmd>(this);
           
            _tokenSource = new CancellationTokenSource();
            var cmd = new TestTokenCancellableCmd(false, CorrelationId.NewId(), SourceId.NullSourceId(), _tokenSource.Token);
            _bus.Fire(cmd);
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _success) == 1, msg: "Success not triggered");
        }

        [Fact]
        public void can_cancel_while_processing()
        {
            _bus.Subscribe<TestTokenCancellableLongRunningCmd>(this);
            _tokenSource = new CancellationTokenSource();
            var cmd = new TestTokenCancellableLongRunningCmd(false, CorrelationId.NewId(), SourceId.NullSourceId(), _tokenSource.Token);

            _bus.TryFire(cmd);
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _gotCmd) == 1, msg: "Command not handled");
            Assert.IsOrBecomesTrue(() => Interlocked.Read(ref _canceled) == 1, msg: "Command not canceled");
        }

        public CommandResponse Handle(TestTokenCancellableCmd command)
        {

            if (command.IsCanceled)
                return command.Fail();
            else
            {
                Interlocked.Exchange(ref _success, 1);
                return command.Succeed();
            }
        }
        public CommandResponse Handle(TestTokenCancellableLongRunningCmd command)
        {
            Interlocked.Exchange(ref _gotCmd, 1);
            _tokenSource.Cancel();
            SpinWait.SpinUntil(() => command.IsCanceled, 500);


            if (command.IsCanceled)
            {
                Interlocked.Exchange(ref _canceled, 1);
                return command.Fail();
            }
            else
            {
                Interlocked.Exchange(ref _success, 1);
                return command.Succeed();
            }
        }
    }
    // ReSharper disable once InconsistentNaming
    public class can_cancel_nested_commands_via_cancellation_token :
        IHandleCommand<TestTokenCancellableCmd>,
        IHandleCommand<NestedTestTokenCancellableCmd>
    {
        private readonly CommandBus _bus;
        public can_cancel_nested_commands_via_cancellation_token()
        {
            _bus = new CommandBus(
                            nameof(can_cancel_nested_commands_via_cancellation_token),
                            3,
                            false,
                            TimeSpan.FromSeconds(2.5),
                            TimeSpan.FromSeconds(2.5));
            Given();
        }

        protected void Given()
        {
            _bus.Subscribe<TestTokenCancellableCmd>(this);
            _bus.Subscribe<NestedTestTokenCancellableCmd>(this);
        }

        protected CancellationTokenSource TokenSource;
        private bool _cancelFirst;
        [Fact]
        public void cancel_will_short_circuit_nested_commands()
        {
            _cancelFirst = true;
            TokenSource = new CancellationTokenSource();
            Assert.CommandThrows<CommandCanceledException>(() =>
            {
                _bus.Fire(
                    new TestTokenCancellableCmd(false, CorrelationId.NewId(), SourceId.NullSourceId(), TokenSource.Token));
            });
            Assert.True(Interlocked.Read(ref _gotCmd) == 1, "Failed to receive first cmd");
            Assert.True(Interlocked.Read(ref _gotNestedCmd) == 0, "Nested Command Fired");

        }
        [Fact]
        public void cancel_will_cancel_nested_commands()
        {
            TokenSource = new CancellationTokenSource();
            Assert.CommandThrows<CommandCanceledException>(() =>
            {
                _bus.Fire(
                    new TestTokenCancellableCmd(false, CorrelationId.NewId(), SourceId.NullSourceId(), TokenSource.Token));
            });
            Assert.True(Interlocked.Read(ref _gotCmd) == 1, "Failed to receive first cmd");
            Assert.True(Interlocked.Read(ref _gotNestedCmd) == 1, "Nested Command received");

        }


        private long _gotCmd;

        public CommandResponse Handle(TestTokenCancellableCmd command)
        {

            Interlocked.Increment(ref _gotCmd);
            var nestedCommand = new NestedTestTokenCancellableCmd(
                                        CorrelationId.NewId(), 
                                        new SourceId(command),
                                        command.CancellationToken);
            if (_cancelFirst)
            {
                TokenSource.Cancel(); //global cancel
                _bus.Fire(nestedCommand); // a pre canceled command will just return
            }
            else
            {
                Assert.CommandThrows<CommandCanceledException>(
                    () =>
                    {
                        _bus.Fire(nestedCommand);
                    });
            }
            return command.IsCanceled ? command.Canceled() : command.Succeed();
        }

        private long _gotNestedCmd;
        public CommandResponse Handle(NestedTestTokenCancellableCmd command)
        {
            Interlocked.Increment(ref _gotNestedCmd);
            TokenSource.Cancel();
            return command.IsCanceled ? command.Canceled() : command.Succeed();
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
        private readonly Dictionary<Guid, ManualResetEventSlim> _parkedMessages = new Dictionary<Guid, ManualResetEventSlim>();
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
    public class TestTokenCancellableCmd : Command
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId => TypeId;

        public readonly bool RequestFail;
        public TestTokenCancellableCmd(
            bool requestFail,
            CorrelationId correlationId,
            SourceId sourceId,
            CancellationToken cancellationToken) :
            base(correlationId, sourceId, cancellationToken)
        {
            RequestFail = requestFail;
        }

    }
    public class TestTokenCancellableLongRunningCmd : Command
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId => TypeId;

        public readonly bool RequestFail;
        public TestTokenCancellableLongRunningCmd(
            bool requestFail,
            CorrelationId correlationId,
            SourceId sourceId,
            CancellationToken cancellationToken) :
            base(correlationId, sourceId, cancellationToken)
        {
            RequestFail = requestFail;
        }

    }
    public class NestedTestTokenCancellableCmd : Command
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId => TypeId;

        public NestedTestTokenCancellableCmd(
            CorrelationId correlationId,
            SourceId sourceId,
            CancellationToken? cancellationToken) :
            base(correlationId, sourceId, cancellationToken)
        {
        }

    }
}
