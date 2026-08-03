using System.Reactive;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests;

// ReSharper disable once InconsistentNaming
public sealed class when_using_category_stream : IClassFixture<StreamStoreConnectionFixture>, IDisposable {
	private static readonly IEventSerializer _serializer = new JsonMessageSerializer();

	private readonly IStreamStoreConnection _store;
	private readonly CountingConnection _counter;
	private readonly IConfiguredConnection _connection;
	private readonly IStreamNameBuilder _namer;
	private readonly string _categoryStream;
	private readonly string _aggregateStream;
	private readonly List<CategoryStream<CategoryStreamTestAggregate>> _streams = [];
	private readonly List<TestSubscriber> _subscribers = [];

	public when_using_category_stream(StreamStoreConnectionFixture fixture) {
		_store = fixture.Connection;
		_store.Connect();
		// Every test instance gets its own category, so tests sharing the fixture's store never see each
		// other's events and the read counts belong to this test alone.
		_namer = new PrefixedCamelCaseStreamNameBuilder(Guid.NewGuid().ToString("N"));
		_counter = new CountingConnection(_store);
		_connection = new ConfiguredConnection(_counter, _namer, _serializer);
		_categoryStream = _namer.GenerateForCategory(typeof(CategoryStreamTestAggregate));
		_aggregateStream = _namer.GenerateForAggregate(typeof(CategoryStreamTestAggregate), Guid.NewGuid());
	}

	[Fact]
	public void one_store_read_serves_every_subscriber() {
		AppendEvents(6);
		var first = NewSubscriber();
		var second = NewSubscriber();
		var source = NewStream();
		source.RelayTo(first);
		source.RelayTo(second);

		source.Start();

		WaitForGoLive(first, second);
		Assert.Equal([0, 1, 2, 3, 4, 5], first.Received);
		Assert.Equal(first.Received, second.Received);
		// Two subscribers, one pass over the history — the whole point of the type.
		Assert.Equal(1, _counter.PageReadCount(_categoryStream));
	}

	[Fact]
	public async Task start_async_reads_the_category_once_for_every_subscriber() {
		AppendEvents(6);
		var first = NewSubscriber();
		var second = NewSubscriber();
		var source = NewStream();
		source.RelayTo(first);
		source.RelayTo(second);

		await source.StartAsync().WaitAsync(TestTimeouts.ThrottleWaitFor);

		WaitForGoLive(first, second);
		Assert.Equal([0, 1, 2, 3, 4, 5], first.Received);
		Assert.Equal(first.Received, second.Received);
		Assert.Equal(1, _counter.PageReadCount(_categoryStream));
	}

	[Fact]
	public void a_restored_relay_is_gated_and_a_from_scratch_relay_is_not() {
		AppendEvents(6);
		var restored = NewSubscriber();
		var fromScratch = NewSubscriber();
		var source = NewStream();
		source.RelayTo(restored, fromPosition: 2);
		source.RelayTo(fromScratch);

		source.Start();

		WaitForGoLive(restored, fromScratch);
		Assert.Equal([3, 4, 5], restored.Received);
		Assert.Equal([0, 1, 2, 3, 4, 5], fromScratch.Received);
		// One read served both: the from-scratch relay forced the full read and the restored relay
		// dropped what it already held.
		Assert.Equal(1, _counter.PageReadCount(_categoryStream));
		Assert.Equal(0, _counter.FirstPageReadStart(_categoryStream));
	}

	[Fact]
	public void the_read_starts_at_the_lowest_position_any_relay_still_needs() {
		AppendEvents(6);
		var older = NewSubscriber();
		var newer = NewSubscriber();
		var source = NewStream();
		source.RelayTo(older, fromPosition: 1);
		source.RelayTo(newer, fromPosition: 3);

		source.Start();

		WaitForGoLive(older, newer);
		Assert.Equal([2, 3, 4, 5], older.Received);
		Assert.Equal([4, 5], newer.Received);
		Assert.Equal(1, _counter.PageReadCount(_categoryStream));
		// Read from the lowest checkpoint any relay still needs (1), i.e. starting at the next event.
		Assert.Equal(2, _counter.FirstPageReadStart(_categoryStream));
	}

	[Fact]
	public void exactly_one_go_live_is_forwarded_after_the_history() {
		AppendEvents(4);
		var subscriber = NewSubscriber();
		var source = NewStream();
		source.RelayTo(subscriber);

		source.Start();

		WaitForGoLive(subscriber);
		// The go-live lands behind the whole history, never in the middle of it.
		Assert.Equal(4, subscriber.EventsBeforeFirstGoLive);
		// The live phase does not produce a second one — the count consumers rely on stays at one.
		AppendEvents(3, firstValue: 100);
		AssertEx.IsOrBecomesTrue(() => subscriber.Received.Length == 7, TestTimeouts.ThrottleWaitFor);
		Assert.Equal(1, subscriber.GoLives);
	}

	[Fact]
	public void live_events_reach_every_relay_whatever_its_gate() {
		AppendEvents(4);
		var restored = NewSubscriber();
		var fromScratch = NewSubscriber();
		var source = NewStream();
		source.RelayTo(restored, fromPosition: 1);
		source.RelayTo(fromScratch);
		source.Start();
		WaitForGoLive(restored, fromScratch);

		AppendEvents(2, firstValue: 100);

		AssertEx.IsOrBecomesTrue(() => restored.Received.Length == 4 && fromScratch.Received.Length == 6,
			TestTimeouts.ThrottleWaitFor);
		// The gate applies to the catch-up read only: live events are past every checkpoint by
		// construction, so both relays see them.
		Assert.Equal([2, 3, 100, 101], restored.Received);
		Assert.Equal([0, 1, 2, 3, 100, 101], fromScratch.Received);
		Assert.Equal(1, restored.GoLives);
		Assert.Equal(1, fromScratch.GoLives);
	}

	[Fact]
	public void position_at_go_live_is_the_last_position_read() {
		AppendEvents(6);
		var subscriber = NewSubscriber();
		var source = NewStream();
		source.RelayTo(subscriber);

		source.Start();

		WaitForGoLive(subscriber);
		// Six events at category positions 0-5.
		Assert.Equal(5L, source.PositionAtGoLive);
	}

	[Fact]
	public void position_at_go_live_is_null_when_nothing_was_read() {
		var subscriber = NewSubscriber();
		var source = NewStream();
		source.RelayTo(subscriber);

		source.Start();

		WaitForGoLive(subscriber);
		Assert.Empty(subscriber.Received);
		Assert.Null(source.PositionAtGoLive);
	}

	[Fact]
	public void a_relay_attached_after_start_misses_the_history_and_the_go_live() {
		AppendEvents(3);
		var early = NewSubscriber();
		var source = NewStream();
		source.RelayTo(early);
		source.Start();
		WaitForGoLive(early);

		// Attaching late does not throw — and this is exactly what it costs, which is why the rule is
		// "attach every relay, then start".
		var late = NewSubscriber();
		source.RelayTo(late);
		AppendEvents(2, firstValue: 100);

		AssertEx.IsOrBecomesTrue(() => late.Received.Length == 2 && early.Received.Length == 5,
			TestTimeouts.ThrottleWaitFor);
		Assert.Equal([100, 101], late.Received);
		Assert.Equal(0, late.GoLives);
		Assert.Equal([0, 1, 2, 100, 101], early.Received);
	}

	[Fact]
	public void disposing_a_relay_stops_delivery_to_that_target() {
		AppendEvents(2);
		var kept = NewSubscriber();
		var dropped = NewSubscriber();
		var source = NewStream();
		source.RelayTo(kept);
		var subscription = source.RelayTo(dropped);
		source.Start();
		WaitForGoLive(kept, dropped);

		subscription.Dispose();
		AppendEvents(2, firstValue: 100);

		AssertEx.IsOrBecomesTrue(() => kept.Received.Length == 4, TestTimeouts.ThrottleWaitFor);
		Assert.Equal([0, 1, 100, 101], kept.Received);
		Assert.Equal([0, 1], dropped.Received);
	}

	[Fact]
	public void starting_twice_throws() {
		AppendEvents(2);
		var subscriber = NewSubscriber();
		var source = NewStream();
		source.RelayTo(subscriber);
		source.Start();
		WaitForGoLive(subscriber);

		Assert.Throws<InvalidOperationException>(() => source.Start());
		Assert.Equal(1, _counter.PageReadCount(_categoryStream));
	}

	private CategoryStream<CategoryStreamTestAggregate> NewStream() {
		var source = new CategoryStream<CategoryStreamTestAggregate>(_connection);
		_streams.Add(source);
		return source;
	}

	private TestSubscriber NewSubscriber() {
		var subscriber = new TestSubscriber($"subscriber-{_subscribers.Count}", _connection);
		_subscribers.Add(subscriber);
		return subscriber;
	}

	private static void WaitForGoLive(params TestSubscriber[] subscribers) =>
		AssertEx.IsOrBecomesTrue(() => subscribers.All(s => s.GoLives == 1), TestTimeouts.ThrottleWaitFor,
			"Expected every subscriber to be handed exactly one go-live.");

	private void AppendEvents(int count, int firstValue = 0) {
		var existing = _store.ReadStreamForward(_aggregateStream, 0, 500)?.Events.Length ?? 0;
		for (var i = 0; i < count; i++) {
			_store.AppendToStream(_aggregateStream, ExpectedVersion.Any, null,
				_serializer.Serialize(new CategoryStreamTestEvent(firstValue + i)));
		}

		_store.TryConfirmStream(_categoryStream, existing + count);
	}

	public void Dispose() {
		_streams.ForEach(s => s.Dispose());
		_subscribers.ForEach(s => s.Dispose());
	}

	public record CategoryStreamTestEvent(int Value) : Event;

	public class CategoryStreamTestAggregate : EventDrivenStateMachine;

	/// <summary>A read model that records what a relay handed it, and when it was told the source is live.</summary>
	private sealed class TestSubscriber : ReadModelBase,
		IHandle<CategoryStreamTestEvent>,
		IHandle<StreamStoreMsgs.CatchupSubscriptionBecameLive> {
		private readonly List<int> _received = [];
		private int _goLives;
		private int _eventsBeforeFirstGoLive = -1;

		public TestSubscriber(string name, IConfiguredConnection connection) : base(name, connection) {
			EventStream.Subscribe<CategoryStreamTestEvent>(this);
			EventStream.Subscribe<StreamStoreMsgs.CatchupSubscriptionBecameLive>(this);
		}

		public int[] Received {
			get {
				lock (ReaderLock) {
					return _received.ToArray();
				}
			}
		}

		public int GoLives {
			get {
				lock (ReaderLock) {
					return _goLives;
				}
			}
		}

		public int EventsBeforeFirstGoLive {
			get {
				lock (ReaderLock) {
					return _eventsBeforeFirstGoLive;
				}
			}
		}

		public void Handle(CategoryStreamTestEvent @event) => _received.Add(@event.Value);

		public void Handle(StreamStoreMsgs.CatchupSubscriptionBecameLive message) {
			if (_goLives == 0)
				_eventsBeforeFirstGoLive = _received.Count;
			_goLives++;
		}
	}

	/// <summary>
	/// Counts the reads that reach the store, so "one read serves N subscribers" is asserted against the
	/// store rather than inferred. Page reads (the ones that carry history) are counted apart from the
	/// single-event probes a reader uses to check that a stream exists.
	/// </summary>
	private sealed class CountingConnection(IStreamStoreConnection inner) : IStreamStoreConnection {
		private readonly object _countLock = new();
		private readonly Dictionary<string, List<long>> _pageReadStarts = [];

		public int PageReadCount(string stream) {
			lock (_countLock) {
				return _pageReadStarts.TryGetValue(stream, out var starts) ? starts.Count : 0;
			}
		}

		public long FirstPageReadStart(string stream) {
			lock (_countLock) {
				return _pageReadStarts[stream][0];
			}
		}

		public StreamEventsSlice? ReadStreamForward(string stream, long start, long count,
			UserCredentials? credentials = null) {
			if (count > 1) {
				lock (_countLock) {
					if (!_pageReadStarts.TryGetValue(stream, out var starts)) {
						starts = [];
						_pageReadStarts.Add(stream, starts);
					}

					starts.Add(start);
				}
			}

			return inner.ReadStreamForward(stream, start, count, credentials);
		}

		public string ConnectionName => inner.ConnectionName;
		public void Connect() => inner.Connect();
		public void Close() => inner.Close();

		public WriteResult AppendToStream(string stream, long expectedVersion, UserCredentials? credentials = null,
			params EventData[] events) =>
			inner.AppendToStream(stream, expectedVersion, credentials, events);

		public StreamEventsSlice? ReadStreamBackward(string stream, long start, long count,
			UserCredentials? credentials = null) =>
			inner.ReadStreamBackward(stream, start, count, credentials);

		public IDisposable SubscribeToStream(string stream, Action<RecordedEvent> eventAppeared,
			Action<SubscriptionDropReason, Exception?>? subscriptionDropped = null,
			UserCredentials? credentials = null) =>
			inner.SubscribeToStream(stream, eventAppeared, subscriptionDropped, credentials);

		public IDisposable SubscribeToStreamFrom(string stream, long? lastCheckpoint,
			CatchUpSubscriptionSettings? settings, Action<RecordedEvent> eventAppeared,
			Action<Unit>? liveProcessingStarted = null,
			Action<SubscriptionDropReason, Exception?>? subscriptionDropped = null,
			UserCredentials? credentials = null) =>
			inner.SubscribeToStreamFrom(stream, lastCheckpoint, settings, eventAppeared, liveProcessingStarted,
				subscriptionDropped, credentials);

		public IDisposable SubscribeToAll(Action<RecordedEvent> eventAppeared,
			Action<SubscriptionDropReason, Exception?>? subscriptionDropped = null,
			UserCredentials? credentials = null, bool resolveLinkTos = true) =>
			inner.SubscribeToAll(eventAppeared, subscriptionDropped, credentials, resolveLinkTos);

		public IDisposable SubscribeToAllFrom(Position from, Action<RecordedEvent> eventAppeared,
			CatchUpSubscriptionSettings? settings = null, Action? liveProcessingStarted = null,
			Action<SubscriptionDropReason, Exception?>? subscriptionDropped = null,
			UserCredentials? credentials = null, bool resolveLinkTos = true) =>
			inner.SubscribeToAllFrom(from, eventAppeared, settings, liveProcessingStarted, subscriptionDropped,
				credentials, resolveLinkTos);

		public void DeleteStream(string stream, long expectedVersion, UserCredentials? credentials = null) =>
			inner.DeleteStream(stream, expectedVersion, credentials);

		public void HardDeleteStream(string stream, long expectedVersion, UserCredentials? credentials = null) =>
			inner.HardDeleteStream(stream, expectedVersion, credentials);

		// The fixture owns the underlying connection's lifetime; this wrapper is a counter, not an owner.
		public void Dispose() { }
	}
}
