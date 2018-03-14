using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Messaging.Testing;
using Xunit;
using Xunit.Abstractions;

namespace ReactiveDomain.Messaging.Tests
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
	public class can_cancel_nested_commands_via_cancellation_token :
		IHandle<Message>,
		IHandleCommand<TestTokenCancellableCmd>,
		IHandleCommand<NestedTestTokenCancellableCmd>
	{
		private readonly ITestOutputHelper _output;
		private readonly CommandBus _bus;
		private readonly ConcurrentMessageQueue<Message> _messages =
								new ConcurrentMessageQueue<Message>("");
		public can_cancel_nested_commands_via_cancellation_token(ITestOutputHelper output)
		{
			_output = output;
			_bus = new CommandBus(
							nameof(can_cancel_nested_commands_via_cancellation_token),
							false,
							TimeSpan.FromSeconds(2.5),
							TimeSpan.FromSeconds(2.5));
			Given();
			When();
		}

		protected void Given()
		{
			_bus.Subscribe<TestTokenCancellableCmd>(this);
			_bus.Subscribe<NestedTestTokenCancellableCmd>(this);
			_bus.Subscribe<Message>(this);
		}

		protected CancellationTokenSource TokenSource;

		protected void When()
		{
			TokenSource = new CancellationTokenSource();
			_bus.TryFire(
				new TestTokenCancellableCmd(false, Guid.NewGuid(), Guid.Empty, TokenSource.Token));
		}
		[Fact]
		public void cancel_will_short_circuit_nested_commands()
		{
			SpinWait.SpinUntil(() => Interlocked.Read(ref _gotCmd) == 1, 5000);
			Assert.True( Interlocked.Read(ref _gotCmd) == 1, "Failed to receive first cmd");
			
			SpinWait.SpinUntil(() => Interlocked.Read(ref _ackCount) == 1, 5000);
			TokenSource.Cancel();
			Interlocked.Increment(ref _releaseCmd);

			SpinWait.SpinUntil(() => Interlocked.Read(ref _gotNestedCmd) == 1, 500);
			Assert.True(Interlocked.Read(ref _gotNestedCmd) == 0, "Received nested cmd");
			

			_messages
				.AssertNext<AckCommand>(_ => true)
				.AssertNext<TestTokenCancellableCmd>(_ => true)
				.AssertNext<Canceled>(_ => true)
				.AssertNext<Fail>(_ => true)
				.AssertNext<Canceled>(_ => true)
				.AssertEmpty();
		
		}
		[Fact]
		public void cancel_will_cancel_nested_commands()
		{
			SpinWait.SpinUntil(() => Interlocked.Read(ref _gotCmd) == 1, 5000);
			Assert.True( Interlocked.Read(ref _gotCmd) == 1, "Failed to receive first cmd");
			Interlocked.Increment(ref _releaseCmd);
			SpinWait.SpinUntil(() => Interlocked.Read(ref _gotNestedCmd) == 1, 5000);
			Assert.True(Interlocked.Read(ref _gotNestedCmd) == 1, "Failed to receive nested cmd");

			SpinWait.SpinUntil(() => Interlocked.Read(ref _ackCount) == 2, 5000);
			TokenSource.Cancel();
			Interlocked.Increment(ref _releaseNestedCmd);
			SpinWait.SpinUntil(() => Interlocked.Read(ref _cancelCount) == 2, 5000);

			_messages
				.AssertNext<AckCommand>(_ => true)
				.AssertNext<TestTokenCancellableCmd>(_ => true)
				.AssertNext<Canceled>(_ => true)
				.AssertNext<AckCommand>(_ => true)
				.AssertNext<NestedTestTokenCancellableCmd>(_ => true)
				.AssertNext<Canceled>(_ => true)
				.AssertNext<Fail>(_ => true)
				.AssertEmpty();
		}

		private long _ackCount;
		private long _cancelCount;
		public void Handle(Message msg) {
			if (msg is TokenCancellableCommand) return;
			_messages.Enqueue(msg);
			if (msg is AckCommand) Interlocked.Increment(ref _ackCount);
			if (msg is Canceled) Interlocked.Increment(ref _cancelCount);
		}
		private long _gotCmd;
		private long _releaseCmd;
		public CommandResponse Handle(TestTokenCancellableCmd command)
		{
			
			_messages.Enqueue(command);
			Interlocked.Increment(ref _gotCmd);
			SpinWait.SpinUntil(() => Interlocked.Read(ref _releaseCmd) == 1, 5000);
			if (Interlocked.Read(ref _releaseCmd) != 1)
				return command.Fail(new TimeoutException());

			_bus.Fire(new NestedTestTokenCancellableCmd(Guid.NewGuid(), command.MsgId, command.CancellationToken));

			return command.IsCanceled ? command.Canceled() : command.Succeed();
		}

		private long _gotNestedCmd;
		private long _releaseNestedCmd;
		public CommandResponse Handle(NestedTestTokenCancellableCmd command)
		{
			_messages.Enqueue(command);
			Interlocked.Increment(ref _gotNestedCmd);
			SpinWait.SpinUntil(() => Interlocked.Read(ref _releaseNestedCmd) == 1, 5000);
			if (Interlocked.Read(ref _releaseNestedCmd) != 1)
				return command.Fail(new TimeoutException());

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
