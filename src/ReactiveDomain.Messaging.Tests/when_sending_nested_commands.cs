using System.Runtime.CompilerServices;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests;

/// <summary>
/// A response-bearing command sent from inside a handler on one of the publisher's queue threads runs
/// inline on that thread. Queued, it could land on the queue its blocked sender occupies and sit there
/// until the ack timeout failed it; with one queue it always did. Fire-and-forget and queue-affine
/// sends keep their queued semantics.
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class when_sending_nested_commands : IDisposable {
	// Short, so a child that queued behind its parent fails fast instead of passing slowly.
	private static readonly TimeSpan AckTimeout = TimeSpan.FromMilliseconds(250);
	private static readonly TimeSpan ResponseTimeout = TimeSpan.FromSeconds(10);

	private readonly List<IDisposable> _disposables = [];

	public enum SendKind { Send, TrySend, SendAsync }

	private static CommandResponse SendChild(IDispatcher dispatcher, ICommand child, SendKind kind) {
		switch (kind) {
			case SendKind.Send:
				dispatcher.Send(child);
				return child.Succeed();
			case SendKind.TrySend:
				dispatcher.TrySend(child, out var response);
				return response;
			default:
				return dispatcher.SendAsync(child).GetAwaiter().GetResult();
		}
	}

	/// <summary>Handles the parent by sending the child, and records which thread handled each.</summary>
	private sealed class Handlers : IHandleCommand<TestCommands.Command1>, IHandleCommand<TestCommands.Command2> {
		private readonly IDispatcher _dispatcher;
		private readonly SendKind _kind;
		private int _parentThreadId;
		private int _childThreadId;

		public Handlers(IDispatcher dispatcher, SendKind kind) {
			_dispatcher = dispatcher;
			_kind = kind;
			dispatcher.Subscribe<TestCommands.Command1>(this);
			dispatcher.Subscribe<TestCommands.Command2>(this);
		}

		public int ParentThreadId => Volatile.Read(ref _parentThreadId);
		public int ChildThreadId => Volatile.Read(ref _childThreadId);

		/// <summary>Runs on the parent's thread before the child is sent.</summary>
		public Action? BeforeSendingChild { get; set; }

		public CommandResponse Handle(TestCommands.Command1 parent) {
			Volatile.Write(ref _parentThreadId, Environment.CurrentManagedThreadId);
			BeforeSendingChild?.Invoke();
			var childResponse = SendChild(_dispatcher, new TestCommands.Command2(), _kind);
			return childResponse is Success
				? parent.Succeed()
				: parent.Fail((childResponse as Fail)?.Exception);
		}

		public CommandResponse Handle(TestCommands.Command2 child) {
			Volatile.Write(ref _childThreadId, Environment.CurrentManagedThreadId);
			return child.Succeed();
		}
	}

	private Dispatcher NewDispatcher(uint queueCount, [CallerMemberName] string name = "") {
		var dispatcher = new Dispatcher(name, queueCount, false, defaultAckTimeout: AckTimeout, defaultResponseTimeout: ResponseTimeout);
		_disposables.Add(dispatcher);
		return dispatcher;
	}

	[Theory]
	[InlineData(1u, SendKind.Send)]
	[InlineData(1u, SendKind.TrySend)]
	[InlineData(1u, SendKind.SendAsync)]
	[InlineData(2u, SendKind.Send)]
	[InlineData(3u, SendKind.TrySend)]
	[InlineData(3u, SendKind.SendAsync)]
	public void a_nested_send_completes_without_waiting_for_a_timeout(uint queueCount, SendKind kind) {
		var dispatcher = NewDispatcher(queueCount);
		_ = new Handlers(dispatcher, kind);

		dispatcher.TrySend(new TestCommands.Command1(), out var response);

		Assert.IsType<Success>(response);
	}

	[Fact]
	public void the_child_runs_on_the_parents_queue_thread() {
		var dispatcher = NewDispatcher(3);
		var handlers = new Handlers(dispatcher, SendKind.TrySend);

		dispatcher.TrySend(new TestCommands.Command1(), out var response);

		Assert.IsType<Success>(response);
		Assert.NotEqual(Environment.CurrentManagedThreadId, handlers.ParentThreadId);
		Assert.Equal(handlers.ParentThreadId, handlers.ChildThreadId);
	}

	[Fact]
	public async Task every_queue_thread_busy_with_a_parent_still_completes() {
		const int queueCount = 3;
		var dispatcher = NewDispatcher(queueCount);
		using var allParentsStarted = new CountdownEvent(queueCount);
		var handlers = new Handlers(dispatcher, SendKind.TrySend) {
			BeforeSendingChild = () => {
				allParentsStarted.Signal();
				allParentsStarted.Wait(ResponseTimeout);
			}
		};

		var responses = await Task.WhenAll(
			Enumerable.Range(0, queueCount).Select(_ => dispatcher.SendAsync(new TestCommands.Command1())));

		Assert.All(responses, response => Assert.IsType<Success>(response));
		Assert.NotEqual(Environment.CurrentManagedThreadId, handlers.ParentThreadId);
	}

	/// <summary>
	/// TrySendAsync promises to enqueue and return. Run inline instead, the child's whole duration
	/// would land inside the sender's round trip, and a child that blocks on state its sender releases
	/// after the call would hang the queue thread.
	/// </summary>
	[Fact]
	public void a_nested_fire_and_forget_returns_before_the_child_runs() {
		var dispatcher = NewDispatcher(1);
		var handlers = new NestedHandlers(dispatcher, d => {
			d.TrySendAsync(new TestCommands.Command2());
			return null;
		});

		dispatcher.TrySend(new TestCommands.Command1(), out var response);

		Assert.IsType<Success>(response);
		Assert.False(handlers.ChildRanDuringParent);
		AssertEx.IsOrBecomesTrue(() => handlers.ChildThreadId != 0, ResponseTimeout, "the child was never handled");
	}

	/// <summary>
	/// The queues are what serialize a queue-affine command against its key, so a nested one is never
	/// pulled inline; with one queue that means it waits behind its blocked sender and the send fails.
	/// Affinity is preserved over liveness.
	/// </summary>
	[Fact]
	public void a_nested_blocking_send_of_a_queue_affine_command_stays_on_its_queue() {
		var dispatcher = NewDispatcher(1);
		var handlers = new NestedHandlers(dispatcher, d => {
			d.TrySend(new AffineCommand(), out var childResponse);
			return childResponse;
		});

		dispatcher.TrySend(new TestCommands.Command1(), out var response);

		Assert.IsType<Fail>(response);
		Assert.False(handlers.ChildRanDuringParent);
	}

	[Fact]
	public void without_queues_parent_and_child_run_on_the_caller() {
		var dispatcher = NewDispatcher(0);
		var handlers = new Handlers(dispatcher, SendKind.Send);

		dispatcher.TrySend(new TestCommands.Command1(), out var response);

		Assert.IsType<Success>(response);
		Assert.Equal(Environment.CurrentManagedThreadId, handlers.ParentThreadId);
		Assert.Equal(Environment.CurrentManagedThreadId, handlers.ChildThreadId);
	}

	public void Dispose() {
		foreach (var disposable in _disposables)
			disposable.Dispose();
	}

	private sealed record AffineCommand : Command, IQueueAffineMessage {
		public int QueueId => 0;
	}

	/// <summary>Handles the parent by running <paramref name="sendChild"/>, recording whether the child had already run when it returned.</summary>
	private sealed class NestedHandlers : IHandleCommand<TestCommands.Command1>, IHandleCommand<TestCommands.Command2>, IHandleCommand<AffineCommand> {
		private readonly IDispatcher _dispatcher;
		private readonly Func<IDispatcher, CommandResponse?> _sendChild;
		private int _childThreadId;

		public NestedHandlers(IDispatcher dispatcher, Func<IDispatcher, CommandResponse?> sendChild) {
			_dispatcher = dispatcher;
			_sendChild = sendChild;
			dispatcher.Subscribe<TestCommands.Command1>(this);
			dispatcher.Subscribe<TestCommands.Command2>(this);
			dispatcher.Subscribe<AffineCommand>(this);
		}

		public int ChildThreadId => Volatile.Read(ref _childThreadId);

		public bool ChildRanDuringParent { get; private set; }

		public CommandResponse Handle(TestCommands.Command1 parent) {
			var childResponse = _sendChild(_dispatcher);
			ChildRanDuringParent = ChildThreadId != 0;
			return childResponse is null or Success
				? parent.Succeed()
				: parent.Fail((childResponse as Fail)?.Exception);
		}

		public CommandResponse Handle(TestCommands.Command2 child) => Counted(child);

		public CommandResponse Handle(AffineCommand child) => Counted(child);

		private CommandResponse Counted(Command child) {
			Volatile.Write(ref _childThreadId, Environment.CurrentManagedThreadId);
			return child.Succeed();
		}
	}
}
