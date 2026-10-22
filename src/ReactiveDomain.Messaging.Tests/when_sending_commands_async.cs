using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests;

/// <summary>
/// <c>SendAsync</c> on the dispatcher. The response semantics are <c>TrySend</c>'s; what these pin on top is
/// which thread does what: the caller is released while a queued handler runs, an unqueued handler runs on
/// the caller, and the continuation never lands on a publisher thread.
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class when_sending_commands_async : IDisposable {
	// Longer than any test here takes, so a command can only time out where a test asks it to.
	private static readonly TimeSpan Generous = TimeSpan.FromSeconds(10);

	private readonly List<IDisposable> _disposables = [];

	private Dispatcher NewDispatcher(uint queueCount, [System.Runtime.CompilerServices.CallerMemberName] string name = "") {
		var dispatcher = new Dispatcher(name, queueCount, false, defaultAckTimeout: Generous, defaultResponseTimeout: Generous);
		_disposables.Add(dispatcher);
		return dispatcher;
	}

	/// <summary>Holds the command until released, so a test can observe the send while the handler is mid-flight.</summary>
	private sealed class GatedHandler : IHandleCommand<TestCommands.Command1>, IDisposable {
		private readonly ManualResetEventSlim _release = new(false);
		private long _started;
		private int _handlerThreadId;

		public long Started => Interlocked.Read(ref _started);
		public int HandlerThreadId => Volatile.Read(ref _handlerThreadId);

		public void Release() => _release.Set();

		public CommandResponse Handle(TestCommands.Command1 command) {
			Volatile.Write(ref _handlerThreadId, Environment.CurrentManagedThreadId);
			Interlocked.Increment(ref _started);
			_release.Wait(Generous);
			return command.Succeed();
		}

		public void Dispose() => _release.Dispose();
	}

	private sealed class ThrowingHandler : IHandleCommand<TestCommands.Throw> {
		public static readonly InvalidOperationException Thrown = new("handler threw");
		public CommandResponse Handle(TestCommands.Throw command) => throw Thrown;
	}

	private sealed class CountingHandler : IHandleCommand<TestCommands.RemoteCancel> {
		private long _handled;
		public long Handled => Interlocked.Read(ref _handled);
		public CommandResponse Handle(TestCommands.RemoteCancel command) {
			Interlocked.Increment(ref _handled);
			return command.Succeed();
		}
	}

	private sealed class CanceledCapture : IHandle<Canceled> {
		private readonly List<Guid> _commandIds = [];
		public void Handle(Canceled message) {
			lock (_commandIds)
				_commandIds.Add(message.CommandId);
		}
		public bool Saw(Guid commandId) {
			lock (_commandIds)
				return _commandIds.Contains(commandId);
		}
	}

	[Fact]
	public async Task queued_send_releases_the_caller_while_the_handler_runs() {
		var dispatcher = NewDispatcher(3);
		using var handler = new GatedHandler();
		dispatcher.Subscribe(handler);

		var task = dispatcher.SendAsync(new TestCommands.Command1());

		AssertEx.IsOrBecomesTrue(() => handler.Started == 1);
		Assert.False(task.IsCompleted);
		Assert.NotEqual(Environment.CurrentManagedThreadId, handler.HandlerThreadId);

		handler.Release();
		var response = await task;
		Assert.IsType<Success>(response);
	}

	[Fact]
	public async Task unqueued_send_runs_the_handler_on_the_calling_thread() {
		var dispatcher = NewDispatcher(0);
		using var handler = new GatedHandler();
		handler.Release();
		dispatcher.Subscribe(handler);

		var task = dispatcher.SendAsync(new TestCommands.Command1());

		Assert.Equal(1, handler.Started);
		Assert.Equal(Environment.CurrentManagedThreadId, handler.HandlerThreadId);
		var response = await task;
		Assert.IsType<Success>(response);
	}

	[Fact]
	public async Task a_handler_that_throws_completes_the_task_with_fail() {
		var dispatcher = NewDispatcher(3);
		dispatcher.Subscribe(new ThrowingHandler());

		var response = await dispatcher.SendAsync(new TestCommands.Throw());

		var fail = Assert.IsType<Fail>(response);
		Assert.Same(ThrowingHandler.Thrown, fail.Exception);
	}

	[Fact]
	public async Task an_unhandled_command_completes_with_fail_on_ack_timeout_and_is_canceled() {
		var dispatcher = NewDispatcher(3);
		var canceled = new CanceledCapture();
		dispatcher.Subscribe(canceled);
		var command = new TestCommands.Unhandled();

		var response = await dispatcher.SendAsync(command, ackTimeout: TimeSpan.FromMilliseconds(100));

		var fail = Assert.IsType<Fail>(response);
		Assert.IsType<CommandNotHandledException>(fail.Exception);
		AssertEx.IsOrBecomesTrue(() => canceled.Saw(command.MsgId));
	}

	[Fact]
	public async Task a_handler_that_overruns_the_response_timeout_completes_with_fail_and_is_canceled() {
		var dispatcher = NewDispatcher(3);
		using var handler = new GatedHandler();
		dispatcher.Subscribe(handler);
		var canceled = new CanceledCapture();
		dispatcher.Subscribe(canceled);
		var command = new TestCommands.Command1();

		var response = await dispatcher.SendAsync(command, responseTimeout: TimeSpan.FromMilliseconds(200));
		handler.Release();

		var fail = Assert.IsType<Fail>(response);
		Assert.IsType<CommandTimedOutException>(fail.Exception);
		AssertEx.IsOrBecomesTrue(() => canceled.Saw(command.MsgId));
	}

	[Fact]
	public async Task a_precanceled_command_completes_with_canceled_and_is_not_handled() {
		var dispatcher = NewDispatcher(3);
		var handler = new CountingHandler();
		dispatcher.Subscribe(handler);
		using var tokenSource = new CancellationTokenSource();
		tokenSource.Cancel();

		var task = dispatcher.SendAsync(new TestCommands.RemoteCancel(tokenSource.Token));

		Assert.True(task.IsCompleted);
		Assert.IsType<Canceled>(await task);
		AssertEx.IsOrBecomesTrue(() => dispatcher.Idle);
		Assert.Equal(0, handler.Handled);
	}

	[Fact]
	public async Task the_continuation_does_not_run_on_a_publisher_thread() {
		var dispatcher = NewDispatcher(3);
		using var handler = new GatedHandler();
		dispatcher.Subscribe(handler);

		// ExecuteSynchronously runs on whichever thread completes the send, so this names that thread.
		var continuation = dispatcher
			.SendAsync(new TestCommands.Command1())
			.ContinueWith(_ => Thread.CurrentThread.Name, TaskContinuationOptions.ExecuteSynchronously);
		AssertEx.IsOrBecomesTrue(() => handler.Started == 1);
		handler.Release();

		var threadName = await continuation;
		Assert.NotEqual("SubscriptionQueue", threadName);
		Assert.NotEqual(nameof(MultiQueuedPublisher), threadName);
	}

	[Fact]
	public async Task concurrent_sends_complete_without_occupying_pool_threads() {
		const int count = 100;
		var dispatcher = NewDispatcher(3);
		dispatcher.Subscribe(new SleepingHandler());
		var poolThreadsBefore = ThreadPool.ThreadCount;

		var responses = await Task.WhenAll(
			Enumerable.Range(0, count).Select(_ => dispatcher.SendAsync(new TestCommands.Command2())));

		Assert.All(responses, response => Assert.IsType<Success>(response));
		// A waiter per send would need a thread per send; awaiting needs none. The bound is loose
		// because the pool also serves the test host.
		Assert.True(ThreadPool.ThreadCount - poolThreadsBefore < count / 2,
			$"thread pool grew by {ThreadPool.ThreadCount - poolThreadsBefore} for {count} sends");
	}

	private sealed class SleepingHandler : IHandleCommand<TestCommands.Command2> {
		public CommandResponse Handle(TestCommands.Command2 command) {
			Thread.Sleep(5);
			return command.Succeed();
		}
	}

	public void Dispose() {
		foreach (var disposable in _disposables)
			disposable.Dispose();
	}
}
