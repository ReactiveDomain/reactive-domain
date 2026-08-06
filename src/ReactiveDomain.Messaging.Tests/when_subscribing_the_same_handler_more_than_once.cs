using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests;

// ReSharper disable once InconsistentNaming
public sealed class when_subscribing_the_same_handler_more_than_once {
	[Fact]
	public void duplicate_subscriptions_are_idempotent() {
		using var bus = new InMemoryBus(nameof(when_subscribing_the_same_handler_more_than_once));
		var handled = 0;
		var handler = new AdHocHandler<ParentTestMessage>(_ => handled++);

		bus.Subscribe(handler);
		bus.Subscribe(handler);
		bus.Publish(new ParentTestMessage());

		Assert.Equal(1, handled);
	}

	/// <summary>The guard runs against every slot a registration covers, not just the declared one.</summary>
	[Fact]
	public void duplicate_subscriptions_are_idempotent_for_derived_types_too() {
		using var bus = new InMemoryBus(nameof(when_subscribing_the_same_handler_more_than_once));
		var handled = 0;
		var handler = new AdHocHandler<ParentTestMessage>(_ => handled++);

		bus.Subscribe(handler);
		bus.Subscribe(handler);
		bus.Publish(new ChildTestMessage());

		Assert.Equal(1, handled);
	}

	[Fact]
	public void the_same_handler_for_a_base_and_a_derived_type_stays_two_registrations() {
		using var bus = new InMemoryBus(nameof(when_subscribing_the_same_handler_more_than_once));
		var handler = new BothLevels();

		bus.Subscribe<ParentTestMessage>(handler);
		bus.Subscribe<ChildTestMessage>(handler);
		bus.Publish(new ChildTestMessage());

		Assert.Equal(1, handler.AsParent);
		Assert.Equal(1, handler.AsChild);
	}

	[Fact]
	public void unsubscribing_a_derived_registration_leaves_the_base_registration() {
		using var bus = new InMemoryBus(nameof(when_subscribing_the_same_handler_more_than_once));
		var handler = new BothLevels();

		bus.Subscribe<ParentTestMessage>(handler);
		bus.Subscribe<ChildTestMessage>(handler);
		bus.Unsubscribe<ChildTestMessage>(handler);
		bus.Publish(new ChildTestMessage());

		Assert.Equal(1, handler.AsParent);
		Assert.Equal(0, handler.AsChild);
	}

	[Fact]
	public void resubscribing_after_unsubscribing_registers_again() {
		using var bus = new InMemoryBus(nameof(when_subscribing_the_same_handler_more_than_once));
		var handled = 0;
		var handler = new AdHocHandler<ParentTestMessage>(_ => handled++);

		bus.Subscribe(handler);
		bus.Unsubscribe(handler);
		bus.Subscribe(handler);
		bus.Publish(new ParentTestMessage());

		Assert.Equal(1, handled);
	}

	[Fact]
	public void either_disposer_releases_the_single_registration() {
		using var bus = new InMemoryBus(nameof(when_subscribing_the_same_handler_more_than_once));
		var handled = 0;
		var handler = new AdHocHandler<ParentTestMessage>(_ => handled++);

		var first = bus.Subscribe(handler);
		var second = bus.Subscribe(handler);

		second.Dispose();
		bus.Publish(new ParentTestMessage());
		Assert.Equal(0, handled);

		first.Dispose();
		bus.Publish(new ParentTestMessage());
		Assert.Equal(0, handled);
	}

	[Fact]
	public void duplicate_subscribe_to_all_is_idempotent() {
		using var bus = new InMemoryBus(nameof(when_subscribing_the_same_handler_more_than_once));
		var handled = 0;
		var handler = new AdHocHandler<IMessage>(_ => handled++);

		bus.SubscribeToAll(handler);
		bus.SubscribeToAll(handler);
		bus.Publish(new ParentTestMessage());

		Assert.Equal(1, handled);
	}

	[Fact]
	public void subscribing_to_all_and_to_a_type_delivers_through_both() {
		using var bus = new InMemoryBus(nameof(when_subscribing_the_same_handler_more_than_once));
		var handler = new AnyAndParent();

		bus.SubscribeToAll(handler);
		bus.Subscribe<ParentTestMessage>(handler);
		bus.Publish(new ParentTestMessage());

		Assert.Equal(1, handler.AsAny);
		Assert.Equal(1, handler.AsParent);
	}

	private sealed class AnyAndParent : IHandle<IMessage>, IHandle<ParentTestMessage> {
		public int AsAny;
		public int AsParent;
		public void Handle(IMessage message) => AsAny++;
		public void Handle(ParentTestMessage message) => AsParent++;
	}

	private sealed class BothLevels : IHandle<ParentTestMessage>, IHandle<ChildTestMessage> {
		public int AsParent;
		public int AsChild;
		public void Handle(ParentTestMessage message) => AsParent++;
		public void Handle(ChildTestMessage message) => AsChild++;
	}
}
