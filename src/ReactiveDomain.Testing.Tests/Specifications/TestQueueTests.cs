using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using Xunit;

// ReSharper disable AccessToDisposedClosure

namespace ReactiveDomain.Testing.Tests.Specifications;

public sealed class TestQueueTests :
	IHandleCommand<TestCommands.Command1>,
	IDisposable {
	private readonly Dispatcher _dispatcher = new("TestQueueTest");
	public TestQueueTests() {
		// ReSharper disable once RedundantTypeArgumentsOfMethod
		_dispatcher.Subscribe<TestCommands.Command1>(this);
	}

	public CommandResponse Handle(TestCommands.Command1 command) {
		return command.Succeed();
	}

	[Fact]
	public void can_wait_on_base_types() {
		using var tq = new TestQueue(_dispatcher);
		Assert.True(_dispatcher.HasSubscriberFor<TestCommands.Command1>());
		var evt = new TestEvent();
		var cmd = new TestCommands.Command1();
		_dispatcher.Publish(evt);
		_dispatcher.Send(cmd);

		tq.WaitFor<IMessage>(TimeSpan.FromMilliseconds(100));
		tq.WaitFor<TestEvent>(TimeSpan.FromMilliseconds(100));
		tq.WaitFor<CommandResponse>(TimeSpan.FromMilliseconds(100));
		tq.WaitFor<Success>(TimeSpan.FromMilliseconds(100));
		tq.WaitFor<TestCommands.Command1>(TimeSpan.FromMilliseconds(100));
		Assert.Throws<TimeoutException>(() => tq.WaitFor<Fail>(TimeSpan.FromMilliseconds(10)));
		tq.AssertNext<TestEvent>(evt.CorrelationId)
			.AssertNext<AckCommand>(cmd.CorrelationId)
			.AssertNext<Success>(cmd.CorrelationId)
			.AssertNext<TestCommands.Command1>(cmd.CorrelationId)
			.AssertEmpty();
	}
	[Fact]
	public void can_get_all_messages_from_queue() {
		using var tq = new TestQueue(_dispatcher);
		Assert.True(_dispatcher.HasSubscriberFor<TestCommands.Command1>());
		var evt = new TestEvent();
		var cmd = new TestCommands.Command1();
		_dispatcher.Publish(evt);
		_dispatcher.Send(cmd);

		tq.WaitFor<CommandResponse>(TimeSpan.FromMilliseconds(100));
		tq.WaitFor<Success>(TimeSpan.FromMilliseconds(100));
		Assert.Throws<TimeoutException>(() => tq.WaitFor<Fail>(TimeSpan.FromMilliseconds(10)));
		tq.AssertNext<TestEvent>(evt.CorrelationId)
			.AssertNext<AckCommand>(cmd.CorrelationId)
			.AssertNext<Success>(cmd.CorrelationId)
			.AssertNext<TestCommands.Command1>(cmd.CorrelationId)
			.AssertEmpty();
	}

	[Fact]
	public void can_restrict_queue_to_commands() {
		using var tq = new TestQueue(_dispatcher, [typeof(Command)]);
		Assert.True(_dispatcher.HasSubscriberFor<TestCommands.Command1>());
		var evt = new TestEvent();
		var cmd = new TestCommands.Command1();
		_dispatcher.Publish(evt);
		_dispatcher.Send(cmd);

		tq.AssertNext<TestCommands.Command1>(cmd.CorrelationId)
			.AssertEmpty();
	}

	[Fact]
	public void can_restrict_queue_to_events() {
		using var tq = new TestQueue(_dispatcher, [typeof(Event)]);
		Assert.True(_dispatcher.HasSubscriberFor<TestCommands.Command1>());
		var evt = new TestEvent();
		var cmd = new TestCommands.Command1();
		_dispatcher.Publish(evt);
		_dispatcher.Send(cmd);

		tq.AssertNext<TestEvent>(evt.CorrelationId)
			.AssertEmpty();
	}

	[Fact]
	public void can_restrict_queue_to_commands_and_events() {
		using var tq = new TestQueue(_dispatcher, [typeof(Event), typeof(Command)]);
		Assert.True(_dispatcher.HasSubscriberFor<TestCommands.Command1>());
		var evt = new TestEvent();
		var cmd = new TestCommands.Command1();
		_dispatcher.Publish(evt);
		_dispatcher.Send(cmd);

		tq.AssertNext<TestEvent>(evt.CorrelationId)
			.AssertNext<TestCommands.Command1>(cmd.CorrelationId)
			.AssertEmpty();
	}
	[Fact]
	public void can_timeout_waiting_for_message_of_type() {
		using var tq = new TestQueue(_dispatcher, [typeof(Event), typeof(Command)]);
		// Don't publish anything
		Assert.Throws<TimeoutException>(() => tq.WaitFor<TestEvent>(TimeSpan.FromMilliseconds(100)));
		tq.AssertEmpty();
	}
	[Fact]
	public async Task waiting_for_message_throws_on_clear() {
		using var tq = new TestQueue(_dispatcher, [typeof(Event), typeof(Command)]);
		await Task.Run(() => Assert.Throws<InvalidOperationException>(() => tq.WaitFor<TestEvent>(TimeSpan.FromMilliseconds(100))))
			.ContinueWith(_ => tq.Clear());
		tq.AssertEmpty();
	}
	[Fact]
	public async Task waiting_for_id_throws_on_clear() {
		using var tq = new TestQueue(_dispatcher, [typeof(Event), typeof(Command)]);
		await Task.Run(() => Assert.Throws<InvalidOperationException>(() => tq.WaitForMsgId(Guid.NewGuid(), TimeSpan.FromMilliseconds(100))))
			.ContinueWith(_ => tq.Clear());
		tq.AssertEmpty();
	}

	[Fact]
	public void can_wait_for_a_specific_message() {
		using var tq = new TestQueue(_dispatcher);
		var evt = new TestEvent();
		var evt2 = new TestEvent();
		//before
		var t1 = Task.Run(() => tq.WaitForMsgId(evt.MsgId, TimeSpan.FromMilliseconds(1000)));
		var t2 = Task.Run(() => tq.WaitForMsgId(evt2.MsgId, TimeSpan.FromMilliseconds(1000)));
		AssertEx.EnsureRunning(t1, t2);

		_dispatcher.Publish(evt);
		_dispatcher.Publish(evt2);

		// or after
		tq.WaitForMsgId(evt2.MsgId, TimeSpan.FromMilliseconds(200));

		tq.AssertNext<TestEvent>(evt.CorrelationId)
			.AssertNext<TestEvent>(evt2.CorrelationId)
			.AssertEmpty();
		AssertEx.EnsureComplete(t1, t2);
	}
	[Fact]
	public void can_wait_for_a_specific_message_twice() {
		using var tq = new TestQueue(_dispatcher);
		var evt = new TestEvent();
		var t1 = Task.Run(() => tq.WaitForMsgId(evt.MsgId, TimeSpan.FromMilliseconds(1000)));
		var t2 = Task.Run(() => tq.WaitForMsgId(evt.MsgId, TimeSpan.FromMilliseconds(1000)));

		tq.Handle(evt);
		tq.AssertNext<TestEvent>(evt.CorrelationId)
			.AssertEmpty();
		AssertEx.EnsureComplete(t1, t2);
	}
	[Fact]
	public void can_wait_for_multiple_message_already_in_the_queue() {
		using var tq = new TestQueue(_dispatcher);
		var evt = new TestEvent();
		var evt2 = new TestEvent();
		_dispatcher.Publish(evt);
		_dispatcher.Publish(evt2);

		tq.WaitForMsgId(evt.MsgId, TimeSpan.FromMilliseconds(200));
		tq.WaitForMsgId(evt2.MsgId, TimeSpan.FromMilliseconds(200));

		tq.AssertNext<TestEvent>(evt.CorrelationId)
			.AssertNext<TestEvent>(evt2.CorrelationId)
			.AssertEmpty();
	}
	[Fact]
	public void can_wait_for_multiple_messages_not_yet_received() {
		using var tq = new TestQueue(_dispatcher);
		var evt = new TestEvent();
		var evt2 = new TestEvent();

		var t1 = Task.Run(() => tq.WaitForMsgId(evt.MsgId, TimeSpan.FromMilliseconds(200)));
		var t2 = Task.Run(() => tq.WaitForMsgId(evt2.MsgId, TimeSpan.FromMilliseconds(200)));

		_dispatcher.Publish(evt);
		_dispatcher.Publish(evt2);
		tq.AssertNext<TestEvent>(evt.CorrelationId)
			.AssertNext<TestEvent>(evt2.CorrelationId)
			.AssertEmpty();
		AssertEx.EnsureComplete(t1, t2);
	}
	[Fact]
	public void can_timeout_waiting_for_message_with_wrong_id() {
		using var tq = new TestQueue(_dispatcher, [typeof(Event), typeof(Command)]);
		var evt = new TestEvent();
		_dispatcher.Publish(evt);

		Assert.Throws<TimeoutException>(() => tq.WaitForMsgId(Guid.NewGuid(), TimeSpan.FromMilliseconds(100)));

		tq.AssertNext<TestEvent>(evt.CorrelationId)
			.AssertEmpty();
	}

	[Fact]
	public void can_wait_by_id_for_a_message_excluded_by_the_type_filter() {
		using var tq = new TestQueue(_dispatcher, [typeof(Command)]);
		var evt = new TestEvent();
		//arrival before the wait starts (the synchronous-delivery fence case)
		_dispatcher.Publish(evt);
		tq.WaitForMsgId(evt.MsgId, TimeSpan.FromMilliseconds(200));

		//arrival after the wait starts
		var evt2 = new TestEvent();
		var t = Task.Run(() => tq.WaitForMsgId(evt2.MsgId, TimeSpan.FromMilliseconds(1000)));
		AssertEx.EnsureRunning(t);
		_dispatcher.Publish(evt2);
		AssertEx.EnsureComplete(t);

		//neither filtered message entered the queue
		tq.AssertEmpty();
	}

	[Fact]
	public void clear_forgets_previously_seen_ids() {
		using var tq = new TestQueue(_dispatcher);
		var evt = new TestEvent();
		_dispatcher.Publish(evt);
		tq.WaitForMsgId(evt.MsgId, TimeSpan.FromMilliseconds(200));
		tq.Clear();
		Assert.Throws<TimeoutException>(() => tq.WaitForMsgId(evt.MsgId, TimeSpan.FromMilliseconds(50)));
		tq.AssertEmpty();
	}

	[Fact]
	public void can_wait_for_multiple_messages_of_a_type() {
		using var tq = new TestQueue(_dispatcher);
		var evt = new TestEvent();
		var evt2 = new TestEvent();
		_dispatcher.Publish(evt);
		_dispatcher.Publish(evt2);

		tq.WaitForMultiple<TestEvent>(2, TimeSpan.FromMilliseconds(100));
		Assert.Throws<TimeoutException>(() => tq.WaitForMultiple<TestEvent>(3, TimeSpan.FromMilliseconds(100)));
		tq.AssertNext<TestEvent>(evt.CorrelationId)
			.AssertNext<TestEvent>(evt2.CorrelationId)
			.AssertEmpty();
	}
	public void Dispose() {
		// ReSharper disable once RedundantTypeArgumentsOfMethod
		_dispatcher.Unsubscribe<TestCommands.Command1>(this);
		_dispatcher.Dispose();
	}
}
