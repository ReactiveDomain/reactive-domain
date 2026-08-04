using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;
// Xunit v3 also declares a TestMessage.
using TestMessage = ReactiveDomain.Testing.TestMessage;

namespace ReactiveDomain.Messaging.Tests;

// ReSharper disable once InconsistentNaming
public sealed class when_reading_registered_message_types {
	[Fact]
	public void a_bus_reports_exactly_the_types_registered() {
		using var bus = new InMemoryBus(nameof(when_reading_registered_message_types));
		Assert.Empty(bus.RegisteredMessageTypes);

		bus.Subscribe(new AdHocHandler<TestMessage>(_ => { }));
		bus.Subscribe(new AdHocHandler<TestMessage2>(_ => { }));
		bus.Subscribe(new AdHocHandler<TestMessage3>(_ => { }));

		Assert.Equal(
			[typeof(TestMessage), typeof(TestMessage2), typeof(TestMessage3)],
			bus.RegisteredMessageTypes.OrderBy(t => t.Name).ToArray());
	}

	[Fact]
	public void derived_types_a_registration_covers_are_not_reported_as_registrations() {
		using var bus = new InMemoryBus(nameof(when_reading_registered_message_types));
		var handled = 0;

		// Subscribing to the parent also routes the two descendants; only the declared type counts.
		bus.Subscribe(new AdHocHandler<ParentTestMessage>(_ => handled++));
		bus.Publish(new GrandChildTestMessage());

		Assert.Equal(1, handled);
		Assert.Equal([typeof(ParentTestMessage)], bus.RegisteredMessageTypes.ToArray());
	}

	/// <summary>Declared as <see cref="IMessage"/>, so it reports as one type, not every type it routes.</summary>
	[Fact]
	public void subscribing_to_all_reports_one_registration() {
		using var bus = new InMemoryBus(nameof(when_reading_registered_message_types));

		bus.SubscribeToAll(new AdHocHandler<IMessage>(_ => { }));

		Assert.Equal([typeof(IMessage)], bus.RegisteredMessageTypes.ToArray());
	}

	[Fact]
	public void unsubscribing_removes_the_type() {
		using var bus = new InMemoryBus(nameof(when_reading_registered_message_types));
		var handler = new AdHocHandler<TestMessage>(_ => { });
		var subscription = bus.Subscribe(handler);
		Assert.Equal([typeof(TestMessage)], bus.RegisteredMessageTypes.ToArray());

		subscription.Dispose();

		Assert.Empty(bus.RegisteredMessageTypes);
	}

	[Fact]
	public void a_queued_subscriber_reports_its_event_and_command_registrations() {
		using var dispatcher = new Dispatcher(nameof(when_reading_registered_message_types));
		using var subscriber = new TestQueuedSubscriber(dispatcher);

		Assert.Equal(
			[typeof(TestCommands.Command1), typeof(TestMessage), typeof(TestMessage2)],
			subscriber.RegisteredMessageTypes.OrderBy(t => t.Name).ToArray());
	}

	private sealed class TestQueuedSubscriber :
		QueuedSubscriber,
		IHandle<TestMessage>,
		IHandle<TestMessage2>,
		IHandleCommand<TestCommands.Command1> {
		public TestQueuedSubscriber(IDispatcher bus) : base(bus) {
			// ReSharper disable RedundantTypeArgumentsOfMethod
			Subscribe<TestMessage>(this);
			Subscribe<TestMessage2>(this);
			Subscribe<TestCommands.Command1>(this);
			// ReSharper restore RedundantTypeArgumentsOfMethod
		}

		public void Handle(TestMessage message) { }
		public void Handle(TestMessage2 message) { }
		public CommandResponse Handle(TestCommands.Command1 command) => command.Succeed();
	}
}
