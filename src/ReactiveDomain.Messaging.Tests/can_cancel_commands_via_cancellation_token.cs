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
	public class can_cancel_concurrent_commands :
		IHandleCommand<TestTokenCancellableCmd>
	{
		private CancellationTokenSource _tokenSource1;
		private CancellationTokenSource _tokenSource2;
		private TestTokenCancellableCmd _cmd1;
		private TestTokenCancellableCmd _cmd2;
		private CommandBus _bus = new CommandBus("test", false);
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
			_cmd1 = new TestTokenCancellableCmd(false, Guid.NewGuid(), Guid.Empty, _tokenSource1.Token);
			_cmd2 = new TestTokenCancellableCmd(false, Guid.NewGuid(), Guid.Empty, _tokenSource2.Token);
			_bus.TryFire(_cmd1);
			_bus.TryFire(_cmd2);
			SpinWait.SpinUntil(() => Interlocked.Read(ref _gotCmd) == 1, 200);
			//Assert.True(Interlocked.Read(ref _gotCmd) == 2, "Didn't get both Commands");
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
			

			if (cmd.IsCanceled) {
				_canceled = cmd.MsgId;
			}
			else {
				_succeeded = cmd.MsgId;
			}
			Interlocked.Increment(ref _completed);
			return cmd.IsCanceled ?  cmd.Canceled() : cmd.Succeed();
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
		IHandleCommand<TestTokenCancellableCmd>,
		IHandleCommand<NestedTestTokenCancellableCmd>
	{
		private readonly CommandBus _bus;
		public can_cancel_nested_commands_via_cancellation_token()
		{
			_bus = new CommandBus(
							nameof(can_cancel_nested_commands_via_cancellation_token),
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
					new TestTokenCancellableCmd(false, Guid.NewGuid(), Guid.Empty, TokenSource.Token));
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
					new TestTokenCancellableCmd(false, Guid.NewGuid(), Guid.Empty, TokenSource.Token));
			});
			Assert.True(Interlocked.Read(ref _gotCmd) == 1, "Failed to receive first cmd");
			Assert.True(Interlocked.Read(ref _gotNestedCmd) == 1, "Nested Command received");

		}


		private long _gotCmd;

		public CommandResponse Handle(TestTokenCancellableCmd command)
		{

			Interlocked.Increment(ref _gotCmd);
			var nestedCommand = new NestedTestTokenCancellableCmd(
				Guid.NewGuid(),
				command.MsgId,
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
