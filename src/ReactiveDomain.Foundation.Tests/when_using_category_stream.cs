using System.Diagnostics.CodeAnalysis;
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

	/// <summary>
	/// The ordering rule is enforced, not merely documented, because getting it wrong fails silently:
	/// a late relay misses the history already read and the go-live already forwarded, and a subscriber
	/// whose liveness is counted one go-live per source would then never go live, with nothing to say
	/// why. The throw is what turns that into a loud failure at the call site.
	/// </summary>
	[Fact]
	public void a_relay_attached_after_start_throws_rather_than_miss_the_history_and_the_go_live() {
		AppendEvents(3);
		var early = NewSubscriber();
		var source = NewStream();
		source.RelayTo(early);
		source.Start();
		WaitForGoLive(early);

		var late = NewSubscriber();
		var thrown = Assert.Throws<InvalidOperationException>(() => source.RelayTo(late));

		// The message has to name the consequence, or the caller learns only that it is not allowed.
		Assert.Contains("history", thrown.Message, StringComparison.OrdinalIgnoreCase);
		Assert.Contains("go-live", thrown.Message, StringComparison.OrdinalIgnoreCase);

		// Refused outright, not half-attached: the live phase reaches the early subscriber and nothing
		// at all reaches the late one.
		AppendEvents(2, firstValue: 100);
		AssertEx.IsOrBecomesTrue(() => early.Received.Length == 5, TestTimeouts.ThrottleWaitFor);
		Assert.Equal([0, 1, 2, 100, 101], early.Received);
		Assert.Empty(late.Received);
		Assert.Equal(0, late.GoLives);
	}

	[Fact]
	public void the_stream_names_the_category_it_reads() {
		// The key half of the checkpoint a consumer stores alongside PositionAtGoLive.
		Assert.Equal(_categoryStream, NewStream().StreamName);
	}

	/// <summary>
	/// A relayed target reads nothing itself, so without the relay registering a source its IsLive
	/// would be vacuously complete — reporting live over a model holding none of the category.
	/// </summary>
	[Fact]
	public async Task a_relayed_target_is_not_live_until_the_relay_has_handed_over_history() {
		AppendEvents(4);
		var subscriber = NewSubscriber();
		var source = NewStream();
		source.RelayTo(subscriber);

		// Registered by RelayTo, so the target is already accounted for before anything is read.
		Assert.False(subscriber.IsLive.IsCompleted);

		source.Start();

		await subscriber.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);
		Assert.Equal(4, subscriber.Received.Length);
	}

	/// <summary>
	/// Detaching before go-live leaves the target unfed, so its registration has to be released or
	/// anyone awaiting it waits for history that is no longer coming.
	/// </summary>
	[Fact]
	public void detaching_a_relay_before_go_live_releases_the_target() {
		var subscriber = NewSubscriber();
		var source = NewStream();
		var subscription = source.RelayTo(subscriber);
		Assert.False(subscriber.IsLive.IsCompleted);

		subscription.Dispose();

		AssertEx.IsOrBecomesTrue(() => subscriber.IsLive.IsCompleted, TestTimeouts.ThrottleWaitFor);
	}

	[Fact]
	public void disposing_the_stream_before_go_live_releases_its_targets() {
		var subscriber = NewSubscriber();
		var source = NewStream();
		source.RelayTo(subscriber);
		Assert.False(subscriber.IsLive.IsCompleted);

		source.Dispose();

		AssertEx.IsOrBecomesTrue(() => subscriber.IsLive.IsCompleted, TestTimeouts.ThrottleWaitFor);
	}

	/// <summary>A second release would retire a source the target still has coming.</summary>
	/// <remarks>
	/// Both releases land on any relay held past go-live — the stream releases it as it forwards the
	/// go-live, the consumer's disposal releases it again — so no concurrency is needed to reach the
	/// guard. The marker event is the barrier: queued behind both, so once it lands the registration
	/// count can be read for real rather than guessed at.
	/// </remarks>
	[Fact]
	public void releasing_a_relay_twice_releases_one_registration() {
		const int marker = -1;
		AppendEvents(1);
		var subscriber = NewSubscriber();
		var held = NewStream();
		var source = NewStream();
		var subscription = source.RelayTo(subscriber);
		// Never started, so nothing but a stray release can retire it.
		held.RelayTo(subscriber);

		source.Start();
		WaitForGoLive(subscriber);
		subscription.Dispose();

		subscriber.Handle(new CategoryStreamTestEvent(marker));
		AssertEx.IsOrBecomesTrue(() => subscriber.Received.Contains(marker), TestTimeouts.ThrottleWaitFor);

		Assert.False(subscriber.IsLive.IsCompleted,
			"releasing one relay twice retired the source still outstanding.");
	}

	/// <summary>
	/// The other half of that checkpoint. A relayed model does not read the category, so the position
	/// it stores is the stream's, and it has to come back on restore without a listener being started
	/// for a stream the category stream already reads.
	/// </summary>
	[Fact]
	public void the_go_live_position_round_trips_as_an_external_checkpoint() {
		AppendEvents(4);
		var subscriber = NewSubscriber();
		var source = NewStream();
		source.RelayTo(subscriber);
		source.Start();
		WaitForGoLive(subscriber);

		var snapshot = new ReadModelState(
			"relayed",
			checkpoints: null,
			state: new object(),
			externalCheckpoints: [new StreamCheckpoint(source.StreamName, source.PositionAtGoLive!.Value)]);

		using var restored = new RelayedSnapshotModel(_connection);
		restored.RestoreFrom(snapshot);

		Assert.True(restored.HasExternalCheckpoint(source.StreamName, out var checkpoint));
		Assert.Equal(source.StreamName, checkpoint.StreamName);
		Assert.Equal(source.PositionAtGoLive!.Value, checkpoint.Version);
		// The category position is this stream's own clock, not an $all position, so there is none.
		Assert.Null(checkpoint.Position);
		// Nothing was started for it: the category stream owns that read.
		Assert.Empty(restored.GetCheckpoint());
	}

	private sealed class RelayedSnapshotModel(IConfiguredConnection connection)
		: SnapshotReadModel(nameof(RelayedSnapshotModel), connection) {
		public void RestoreFrom(ReadModelState snapshot) => Restore(snapshot);

		public bool HasExternalCheckpoint(
			string streamName,
			[NotNullWhen(true)] out StreamCheckpoint? checkpoint) =>
			TryGetExternalCheckpoint(streamName, out checkpoint);

		protected override void ApplyState(ReadModelState snapshot) { }

		public override ReadModelState GetState() =>
			new(nameof(RelayedSnapshotModel), GetCheckpoint(), new object(), GetExternalCheckpoints());
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

	/// <summary>
	/// A relay registers a source on its target before anything is read. If the read then fails, the
	/// target is left holding its liveness open for history that will never be sent, and awaiting it
	/// waits forever — a hang, with nothing to say why.
	/// </summary>
	[Fact]
	public void a_start_that_throws_releases_the_targets_it_registered() {
		AppendEvents(2);
		var subscriber = NewSubscriber();
		var failing = new ConfiguredConnection(
			new CountingConnection(_store, failReadsOn: _categoryStream), _namer, _serializer);
		var source = new CategoryStream<CategoryStreamTestAggregate>(failing);
		_streams.Add(source);
		source.RelayTo(subscriber);
		Assert.False(subscriber.IsLive.IsCompleted);

		Assert.Throws<InvalidOperationException>(() => source.Start());

		AssertEx.IsOrBecomesTrue(() => subscriber.IsLive.IsCompleted, TestTimeouts.ThrottleWaitFor,
			"A failed start left its target waiting on history nobody will send.");
	}

	/// <summary>
	/// A relay's registration is stamped with the generation it was made in, so a release that arrives
	/// after that generation was abandoned cannot retire a source registered since.
	/// </summary>
	/// <remarks>
	/// <para>The abandonment has to come from the <i>target's</i> own start failing. A failure in the
	/// stream's start drains its relays as it leaves, in the generation they were registered in, which
	/// is the ordinary path and proves nothing about the stamp.</para>
	/// <para>The later stream is gated inside its <i>read</i> rather than in a handler. A handler gate
	/// would park the queue thread, so the stale release would sit behind that stream's own sentinel
	/// and be ignored because the count had already reached zero — again proving nothing. Holding the
	/// read open lets the stale release reach an empty queue and a non-zero count, which is the only
	/// arrangement a stamped and an unstamped release disagree on.</para>
	/// </remarks>
	[Fact]
	public void a_relay_released_after_its_generation_was_abandoned_retires_nothing() {
		var failing = _namer.GenerateForAggregate(typeof(CategoryStreamTestAggregate), Guid.NewGuid());
		var gated = _namer.GenerateForAggregate(typeof(CategoryStreamTestAggregate), Guid.NewGuid());
		foreach (var stream in new[] { failing, gated }) {
			_store.AppendToStream(stream, ExpectedVersion.Any, null,
				_serializer.Serialize(new CategoryStreamTestEvent(7)));
		}
		using var readGate = new ManualResetEventSlim(false);
		var counter = new CountingConnection(_store, failReadsOn: failing, blockReadsOn: (gated, readGate));
		var connection = new ConfiguredConnection(counter, _namer, _serializer);

		var subscriber = new TestSubscriber("generation-target", connection);
		_subscribers.Add(subscriber);
		// Never started, so nothing drains this relay but the release below.
		var source = new CategoryStream<CategoryStreamTestAggregate>(connection);
		_streams.Add(source);
		var relay = source.RelayTo(subscriber);

		// The target's own start fails, abandoning every source outstanding — the relay's registration
		// included — and moving the generation on.
		Assert.Throws<InvalidOperationException>(() => subscriber.Start(failing));

		// A stream registered in the new generation, held open inside its read so nothing has queued a
		// sentinel for it yet.
		subscriber.StartAsync(gated);
		var live = subscriber.IsLive;
		AssertEx.IsOrBecomesTrue(() => counter.BlockedReads > 0, TestTimeouts.ThrottleWaitFor,
			"The gated read never started.");

		relay.Dispose();
		// Waited on rather than assumed: the release is a queued message, and asserting before it is
		// handled would pass whether or not it was stamped.
		AssertEx.IsOrBecomesTrue(() => subscriber.Idle && subscriber.MessageCount == 0,
			TestTimeouts.ThrottleWaitFor, "The release was never handled.");

		Assert.False(live.IsCompleted,
			"A release from an abandoned generation retired a stream registered after it.");

		readGate.Set();
		AssertEx.IsOrBecomesTrue(() => live.IsCompleted, TestTimeouts.ThrottleWaitFor);
	}

	/// <summary>
	/// The other half: a stamp that is merely <i>a</i> value rather than the registration's own would
	/// make every release from a later generation a no-op, and its target would never go live.
	/// </summary>
	[Fact]
	public void a_relay_registered_after_an_abandonment_still_retires_its_target() {
		AppendEvents(2);
		// Outside the category, so failing it moves the generation on without adding to what is relayed.
		var failing = $"unrelated-{Guid.NewGuid():N}";
		_store.AppendToStream(failing, ExpectedVersion.Any, null,
			_serializer.Serialize(new CategoryStreamTestEvent(7)));
		var connection = new ConfiguredConnection(
			new CountingConnection(_store, failReadsOn: failing), _namer, _serializer);

		var subscriber = new TestSubscriber("post-abandonment-target", connection);
		_subscribers.Add(subscriber);
		// Moves the generation on before the relay is registered, so the registration's own generation
		// is no longer the first one.
		Assert.Throws<InvalidOperationException>(() => subscriber.Start(failing));

		var source = new CategoryStream<CategoryStreamTestAggregate>(connection);
		_streams.Add(source);
		source.RelayTo(subscriber);
		var live = subscriber.IsLive;

		source.Start();

		AssertEx.IsOrBecomesTrue(() => live.IsCompleted, TestTimeouts.ThrottleWaitFor,
			"The relay handed over its history and released, but the target never went live.");
		Assert.Equal([0, 1], subscriber.Received);
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
	private sealed class CountingConnection(
		IStreamStoreConnection inner,
		string? failReadsOn = null,
		(string Stream, ManualResetEventSlim Gate)? blockReadsOn = null) : IStreamStoreConnection {
		private readonly object _countLock = new();
		private readonly Dictionary<string, List<long>> _pageReadStarts = [];
		private int _blockedReads;

		/// <summary>How many reads have parked on the gate, so a test can wait for one rather than sleep.</summary>
		public int BlockedReads => Volatile.Read(ref _blockedReads);

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
			// Page reads only: the reader probes existence with a single-event read and swallows what
			// that throws, so failing it would never reach the caller. History is read in pages.
			if (stream == failReadsOn && count > 1)
				throw new InvalidOperationException($"Reads of {stream} are failing.");
			if (blockReadsOn is { } blocked && stream == blocked.Stream && count > 1) {
				Interlocked.Increment(ref _blockedReads);
				blocked.Gate.Wait(TestTimeouts.ThrottleWaitFor);
			}
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
