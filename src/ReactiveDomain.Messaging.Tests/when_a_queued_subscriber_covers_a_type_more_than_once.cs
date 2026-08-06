using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests;

// ReSharper disable once InconsistentNaming
public sealed class when_a_queued_subscriber_covers_a_type_more_than_once {
	private sealed class Subscriber : QueuedSubscriber {
		public int Parent, Child, GrandChild;
		private readonly IDisposable? _parentSub, _childSub;

		public Subscriber(IBus bus, bool idempotent = false, bool grandChild = false) : base(bus, idempotent) {
			_parentSub = Subscribe(new AdHocHandler<ParentTestMessage>(_ => Interlocked.Increment(ref Parent)));
			_childSub = Subscribe(new AdHocHandler<ChildTestMessage>(_ => Interlocked.Increment(ref Child)));
			if (grandChild)
				Subscribe(new AdHocHandler<GrandChildTestMessage>(_ => Interlocked.Increment(ref GrandChild)));
		}

		public void DropParent() => _parentSub?.Dispose();
		public void DropChild() => _childSub?.Dispose();
	}

	/// <summary>Reads counters only after the queue has drained, since delivery is asynchronous.</summary>
	private static void Settle(Subscriber s, Func<bool> reached) {
		AssertEx.IsOrBecomesTrue(reached, TestTimeouts.WaitFor);
		AssertEx.IsOrBecomesTrue(() => s.Starving, TestTimeouts.WaitFor);
	}

	[Fact]
	public void a_message_is_delivered_once_per_handler() {
		using var bus = new InMemoryBus(nameof(when_a_queued_subscriber_covers_a_type_more_than_once));
		using var sub = new Subscriber(bus);

		bus.Publish(new ChildTestMessage());
		Settle(sub, () => sub.Child == 1);

		Assert.Equal(1, sub.Parent);
		Assert.Equal(1, sub.Child);
	}

	[Fact]
	public void a_chain_of_covered_types_is_delivered_once_per_handler() {
		using var bus = new InMemoryBus(nameof(when_a_queued_subscriber_covers_a_type_more_than_once));
		using var sub = new Subscriber(bus, grandChild: true);

		bus.Publish(new GrandChildTestMessage());
		Settle(sub, () => sub.GrandChild == 1);

		Assert.Equal(1, sub.Parent);
		Assert.Equal(1, sub.Child);
		Assert.Equal(1, sub.GrandChild);
	}

	[Fact]
	public void dropping_the_covering_subscription_leaves_the_others_receiving() {
		using var bus = new InMemoryBus(nameof(when_a_queued_subscriber_covers_a_type_more_than_once));
		using var sub = new Subscriber(bus);

		sub.DropParent();
		bus.Publish(new ChildTestMessage());
		Settle(sub, () => sub.Child == 1);

		Assert.Equal(0, sub.Parent);
		Assert.Equal(1, sub.Child);
	}

	[Fact]
	public void dropping_a_covered_subscription_leaves_the_covering_one_receiving() {
		using var bus = new InMemoryBus(nameof(when_a_queued_subscriber_covers_a_type_more_than_once));
		using var sub = new Subscriber(bus);

		sub.DropChild();
		bus.Publish(new ChildTestMessage());
		Settle(sub, () => sub.Parent == 1);

		Assert.Equal(1, sub.Parent);
		Assert.Equal(0, sub.Child);
	}

	/// <summary>
	/// Parent covered both others, so the feed must be re-elected on release, not handed back to
	/// each type it displaced — that would deliver to the grandchild handler twice.
	/// </summary>
	[Fact]
	public void handing_the_feed_back_to_a_chain_still_delivers_once() {
		using var bus = new InMemoryBus(nameof(when_a_queued_subscriber_covers_a_type_more_than_once));
		using var sub = new Subscriber(bus, grandChild: true);

		sub.DropParent();
		bus.Publish(new GrandChildTestMessage());
		Settle(sub, () => sub.GrandChild == 1);

		Assert.Equal(0, sub.Parent);
		Assert.Equal(1, sub.Child);
		Assert.Equal(1, sub.GrandChild);
	}

	/// <summary>Idempotent subscribers already masked the duplicate, so they are the regression risk.</summary>
	[Fact]
	public void an_idempotent_subscriber_is_unaffected() {
		using var bus = new InMemoryBus(nameof(when_a_queued_subscriber_covers_a_type_more_than_once));
		using var sub = new Subscriber(bus, idempotent: true);

		bus.Publish(new ChildTestMessage());
		Settle(sub, () => sub.Child == 1);

		Assert.Equal(1, sub.Parent);
		Assert.Equal(1, sub.Child);
	}

	[Fact]
	public void disposing_the_subscriber_twice_after_dropping_a_subscription_does_not_throw() {
		using var bus = new InMemoryBus(nameof(when_a_queued_subscriber_covers_a_type_more_than_once));
		var sub = new Subscriber(bus);

		sub.DropParent();
		sub.Dispose();
		sub.Dispose();

		// Dispose drops the subscriptions before returning, so the publish cannot even enqueue.
		bus.Publish(new ChildTestMessage());

		Assert.Equal(0, sub.Child);
	}

	/// <summary>The shared feed is held by a count, not a flag.</summary>
	[Fact]
	public void dropping_one_of_two_handlers_on_a_type_leaves_the_other_receiving() {
		using var bus = new InMemoryBus(nameof(when_a_queued_subscriber_covers_a_type_more_than_once));
		using var sub = new TwoOnOneType(bus);

		sub.DropFirst();
		bus.Publish(new ParentTestMessage());
		AssertEx.IsOrBecomesTrue(() => sub.Second == 1, TestTimeouts.WaitFor);
		AssertEx.IsOrBecomesTrue(() => sub.Starving, TestTimeouts.WaitFor);

		Assert.Equal(0, sub.First);
		Assert.Equal(1, sub.Second);
	}

	private sealed class TwoOnOneType : QueuedSubscriber {
		public int First, Second;
		private readonly IDisposable _firstSub;

		public TwoOnOneType(IBus bus) : base(bus) {
			_firstSub = Subscribe(new AdHocHandler<ParentTestMessage>(_ => Interlocked.Increment(ref First)));
			Subscribe(new AdHocHandler<ParentTestMessage>(_ => Interlocked.Increment(ref Second)));
		}

		public void DropFirst() => _firstSub.Dispose();
	}
}
