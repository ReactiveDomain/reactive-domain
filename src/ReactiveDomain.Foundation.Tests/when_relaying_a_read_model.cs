using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests;

/// <summary>
/// Covers <see cref="ReadModelBase.RelayTo"/> and <see cref="ReadModelBase.Emit"/>: a change a source
/// raises reaches the target on the target's queue, paired with the source's applied checkpoints,
/// and the target's liveness and cuts account for the source.
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class when_relaying_a_read_model : IClassFixture<StreamStoreConnectionFixture>, IDisposable {
	private const int GatedValue = 97;

	private readonly IStreamStoreConnection _conn;
	private readonly IConfiguredConnection _configured;
	private readonly IEventSerializer _serializer = new JsonMessageSerializer();
	private readonly IStreamNameBuilder _namer =
		new PrefixedCamelCaseStreamNameBuilder(nameof(when_relaying_a_read_model));
	private readonly List<IDisposable> _disposables = [];

	public when_relaying_a_read_model(StreamStoreConnectionFixture fixture) {
		_conn = fixture.Connection;
		_conn.Connect();
		_configured = new ConfiguredConnection(_conn, _namer, _serializer);
	}

	private string NewStream() => _namer.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());

	private void AppendEvents(string streamName, params int[] values) {
		foreach (var value in values) {
			_conn.AppendToStream(streamName, ExpectedVersion.Any, null,
				_serializer.Serialize(new RelayTestEvent(value)));
		}
	}

	private static int[] Ones(int count) => Enumerable.Repeat(1, count).ToArray();

	private T Track<T>(T disposable) where T : IDisposable {
		_disposables.Add(disposable);
		return disposable;
	}

	private SourceModel NewSource(ManualResetEventSlim? handlerGate = null, IConfiguredConnection? connection = null) =>
		Track(new SourceModel($"source-{_disposables.Count}", connection ?? _configured, handlerGate));

	private TargetModel NewTarget(ManualResetEventSlim? handlerGate = null) =>
		Track(new TargetModel($"target-{_disposables.Count}", _configured, handlerGate));

	[Fact]
	public async Task a_relayed_change_carries_the_sources_checkpoints_at_the_emit() {
		var stream = NewStream();
		AppendEvents(stream, Ones(5));
		var source = NewSource();
		var target = NewTarget();
		source.RelayTo(target);

		source.StartAsync(stream);
		await target.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		// Each change is named by the event the source was applying when it raised it.
		Assert.Equal(5, target.Seen.Count);
		for (var i = 0; i < 5; i++) {
			var seen = Assert.Single(target.Seen[i]);
			Assert.Equal(stream, seen.StreamName);
			Assert.Equal(i, seen.Version);
		}
		Assert.Equal(CheckpointOrder.Equal, StreamCheckpoint.Compare(target.Applied, source.Applied));
	}

	[Fact]
	public async Task the_target_is_not_live_until_the_source_has_drained() {
		var stream = NewStream();
		AppendEvents(stream, 1, 1, 1, GatedValue);
		using var handlerGate = new ManualResetEventSlim(false);
		var source = NewSource(handlerGate);
		var target = NewTarget();
		source.RelayTo(target);

		source.StartAsync(stream);
		var live = target.IsLive;

		// Three changes forwarded and the source parked inside its fourth handler, so its transition
		// cannot happen and the target's release cannot be queued. Structural, not a race.
		try {
			Assert.True(source.Parked.Wait(TestTimeouts.ThrottleWaitFor));
			AssertEx.IsOrBecomesTrue(() => target.ReceivedCount == 3, TestTimeouts.ThrottleWaitFor);
			Assert.False(live.IsCompleted);
		} finally {
			handlerGate.Set();
		}

		await live.WaitAsync(TestTimeouts.ThrottleWaitFor);
		Assert.Equal(4, target.ReceivedCount);
		Assert.Equal(3, Assert.Single(target.Applied).Version);
	}

	/// <summary>
	/// A late attach runs on the source's queue behind what is there, and the snapshot it emits names
	/// the position those changes left — so the target's state and its checkpoint agree.
	/// </summary>
	[Fact]
	public async Task attaching_from_a_live_sources_queue_thread_hands_over_its_position() {
		var stream = NewStream();
		AppendEvents(stream, Ones(4));
		using var handlerGate = new ManualResetEventSlim(false);
		var source = NewSource(handlerGate);
		source.StartAsync(stream);
		await source.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		// Parked on a live event with two more queued behind it, then the attach is registered: it
		// runs behind all three.
		AppendEvents(stream, GatedValue, 1, 1);
		var target = NewTarget();
		Task attached;
		try {
			Assert.True(source.Parked.Wait(TestTimeouts.ThrottleWaitFor));
			AssertEx.IsOrBecomesTrue(() => source.MessageCount >= 2, TestTimeouts.ThrottleWaitFor);
			attached = source.AttachOnceLive(target, snapshot: new RelayedChange(0, 0));
			Assert.False(attached.IsCompleted);
		} finally {
			handlerGate.Set();
		}
		await attached.WaitAsync(TestTimeouts.ThrottleWaitFor);
		await target.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		var at = Assert.Single(Assert.Single(target.Seen));
		Assert.Equal(stream, at.StreamName);
		Assert.Equal(6, at.Version);
		Assert.Equal(6, Assert.Single(target.Applied).Version);
	}

	[Fact]
	public async Task attaching_off_the_queue_thread_after_anything_was_folded_is_refused() {
		var stream = NewStream();
		AppendEvents(stream, Ones(2));
		var source = NewSource();
		var target = NewTarget();
		source.StartAsync(stream);
		await source.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		Assert.Throws<InvalidOperationException>(() => source.RelayTo(target));

		// Refused before anything was registered on the target.
		Assert.True(target.IsLive.IsCompleted);
	}

	/// <summary>
	/// Being a target registers on a model without folding anything into it, so a chain is attached
	/// in any order before the first start.
	/// </summary>
	[Fact]
	public async Task a_model_that_is_itself_a_target_relays_on_before_its_source_starts() {
		var stream = NewStream();
		AppendEvents(stream, Ones(3));
		var a = NewSource();
		var b = Track(new ForwardingModel("b", _configured));
		var c = NewTarget();
		a.RelayTo(b);
		b.RelayTo(c);

		a.StartAsync(stream);
		await c.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		Assert.Equal(3, c.ReceivedCount);
		Assert.Equal(2, Assert.Single(c.Applied).Version);
	}

	[Fact]
	public async Task a_source_that_raised_nothing_while_folding_still_seeds_its_streams_at_the_release() {
		var stream = NewStream();
		var source = NewSource();
		var target = NewTarget();
		source.RelayTo(target);

		source.StartAsync(stream);
		await target.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		Assert.Equal(0, target.ReceivedCount);
		var applied = Assert.Single(target.Applied);
		Assert.Equal(stream, applied.StreamName);
		Assert.Null(applied.Version);
	}

	[Fact]
	public async Task a_change_raised_before_the_sources_transition_reaches_the_target_before_its_once_live_callback() {
		var stream = NewStream();
		AppendEvents(stream, Ones(5));
		var source = NewSource();
		var target = NewTarget();
		source.RelayTo(target);
		var seen = -1;
		var once = target.OnceLive(() => seen = target.ReceivedCount);

		source.StartAsync(stream);
		await once.WaitAsync(TestTimeouts.ThrottleWaitFor);

		Assert.Equal(5, seen);
	}

	[Fact]
	public async Task emit_off_the_queue_thread_throws() {
		var stream = NewStream();
		var source = NewSource();
		var target = NewTarget();
		source.RelayTo(target);
		source.StartAsync(stream);
		await target.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		var thrown = Assert.Throws<InvalidOperationException>(() => source.EmitFromHere(new RelayedChange(1, 1)));

		Assert.Contains("queue thread", thrown.Message, StringComparison.OrdinalIgnoreCase);
		Assert.Equal(0, target.ReceivedCount);

		// The queue thread is the rule, not the handler: a callback sequenced there may raise too.
		await source.EmitOnceLive(new RelayedChange(2, 1)).WaitAsync(TestTimeouts.ThrottleWaitFor);
		AssertEx.IsOrBecomesTrue(() => target.ReceivedCount == 1, TestTimeouts.ThrottleWaitFor);
	}

	[Fact]
	public async Task two_sources_sharing_a_stream_report_the_lesser_of_their_positions() {
		var stream = NewStream();
		AppendEvents(stream, 1, 1, GatedValue, 1, 1, 1);
		using var handlerGate = new ManualResetEventSlim(false);
		var slow = NewSource(handlerGate);
		var fast = NewSource();
		var target = NewTarget();
		slow.RelayTo(target);
		fast.RelayTo(target);

		slow.StartAsync(stream);
		fast.StartAsync(stream);
		var live = target.IsLive;

		try {
			// Six changes from the fast source, two from the slow one before it parks: the target has
			// been told version 5 and version 1 for the one stream, and reports the lesser.
			Assert.True(slow.Parked.Wait(TestTimeouts.ThrottleWaitFor));
			AssertEx.IsOrBecomesTrue(() => target.ReceivedCount == 8, TestTimeouts.ThrottleWaitFor);
			var applied = Assert.Single(target.Applied);
			Assert.Equal(stream, applied.StreamName);
			Assert.Equal(1, applied.Version);
			Assert.False(live.IsCompleted);
		} finally {
			handlerGate.Set();
		}

		await live.WaitAsync(TestTimeouts.ThrottleWaitFor);
		Assert.Equal(12, target.ReceivedCount);
		Assert.Equal(5, Assert.Single(target.Applied).Version);
	}

	[Fact]
	public async Task a_cut_on_a_target_with_no_streams_of_its_own_names_the_sources_streams() {
		var stream = NewStream();
		AppendEvents(stream, Ones(5));
		var source = NewSource();
		var target = NewTarget();
		source.RelayTo(target);
		source.StartAsync(stream);
		await target.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		var (checkpoints, count) = await target.Cut().WaitAsync(TestTimeouts.ThrottleWaitFor);

		var cut = Assert.Single(checkpoints);
		Assert.Equal(stream, cut.StreamName);
		Assert.Equal(4, cut.Version);
		Assert.Equal(5, count);
	}

	/// <summary>
	/// The invariant a cut exists for: the checkpoint it hands over names exactly the changes the
	/// state was read with, under traffic. Each change here is the source's <c>n</c>th, raised at
	/// version <c>n - 1</c>, so a cut at <c>n</c> changes must name version <c>n - 1</c>.
	/// </summary>
	[Fact]
	public async Task a_cut_taken_under_relayed_traffic_describes_its_own_cut() {
		var stream = NewStream();
		AppendEvents(stream, Ones(5));
		using var handlerGate = new ManualResetEventSlim(false);
		var source = NewSource();
		var target = NewTarget(handlerGate);
		source.RelayTo(target);
		source.StartAsync(stream);
		await target.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		// Wedged on the sixth change, the burst piles into the target's queue behind it; the cut is
		// requested against that provably non-empty queue, and more arrives behind the barrier.
		AppendEvents(stream, GatedValue);
		AppendEvents(stream, Ones(10));
		Task<(IReadOnlyList<StreamCheckpoint> Checkpoints, int Count)> capturing;
		try {
			Assert.True(target.Parked.Wait(TestTimeouts.ThrottleWaitFor));
			AssertEx.IsOrBecomesTrue(() => target.MessageCount >= 10, TestTimeouts.ThrottleWaitFor);
			capturing = target.Cut();
			AppendEvents(stream, Ones(5));
		} finally {
			handlerGate.Set();
		}

		var (checkpoints, count) = await capturing.WaitAsync(TestTimeouts.ThrottleWaitFor);

		Assert.Equal(16, count);
		Assert.Equal(15, Assert.Single(checkpoints).Version);
		AssertEx.IsOrBecomesTrue(() => target.ReceivedCount == 21, TestTimeouts.ThrottleWaitFor);
	}

	[Fact]
	public async Task disposing_the_relay_before_the_source_is_live_releases_the_target() {
		var stream = NewStream();
		AppendEvents(stream, Ones(3));
		using var readGate = new ManualResetEventSlim(false);
		var connection = new HookedConnection(_configured, beforeRead: s => {
			if (s == stream) { readGate.Wait(TestTimeouts.ThrottleWaitFor); }
		});
		var source = NewSource(connection: connection);
		var target = NewTarget();
		var relay = source.RelayTo(target);
		source.StartAsync(stream);
		var live = target.IsLive;
		Assert.False(live.IsCompleted);

		relay.Dispose();

		await live.WaitAsync(TestTimeouts.ThrottleWaitFor);
		Assert.Empty(target.Applied);

		// Detached, not merely released: the history the source folds afterwards does not reach it.
		readGate.Set();
		await source.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);
		AssertEx.IsOrBecomesTrue(() => source.Idle && target.Idle, TestTimeouts.ThrottleWaitFor);
		Assert.Equal(3, source.Count);
		Assert.Equal(0, target.ReceivedCount);
	}

	[Fact]
	public async Task disposing_the_source_before_its_transition_releases_the_target() {
		var stream = NewStream();
		AppendEvents(stream, Ones(3));
		using var readGate = new ManualResetEventSlim(false);
		var connection = new HookedConnection(_configured, beforeRead: s => {
			if (s == stream) { readGate.Wait(TestTimeouts.ThrottleWaitFor); }
		});
		var source = new SourceModel("disposed-source", connection);
		var target = NewTarget();
		source.RelayTo(target);
		source.StartAsync(stream);
		var live = target.IsLive;
		Assert.False(live.IsCompleted);

		source.Dispose();
		readGate.Set();

		await live.WaitAsync(TestTimeouts.ThrottleWaitFor);
	}

	[Fact]
	public async Task disposing_a_source_that_never_started_releases_the_target() {
		var source = new SourceModel("never-started", _configured);
		var target = NewTarget();
		source.RelayTo(target);
		var live = target.IsLive;
		Assert.False(live.IsCompleted);

		source.Dispose();

		await live.WaitAsync(TestTimeouts.ThrottleWaitFor);
		Assert.Empty(target.Applied);
	}

	[Fact]
	public void relaying_to_a_disposed_target_is_refused() {
		var source = NewSource();
		var target = NewTarget();
		target.Dispose();

		Assert.Throws<ObjectDisposedException>(() => source.RelayTo(target));
	}

	[Fact]
	public void relaying_from_a_disposed_source_is_refused() {
		var source = NewSource();
		var target = NewTarget();
		source.Dispose();

		Assert.Throws<ObjectDisposedException>(() => source.RelayTo(target));

		Assert.True(target.IsLive.IsCompleted);
	}

	[Fact]
	public async Task a_target_disposed_after_attach_is_no_longer_fed() {
		var stream = NewStream();
		AppendEvents(stream, Ones(2));
		var source = NewSource();
		var target = NewTarget();
		source.RelayTo(target);
		source.StartAsync(stream);
		await target.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		target.Dispose();
		AppendEvents(stream, Ones(3));

		AssertEx.IsOrBecomesTrue(() => source.Count == 5 && source.Idle, TestTimeouts.ThrottleWaitFor);
		Assert.Equal(2, target.ReceivedCount);
		Assert.Equal(0, target.MessageCount);
	}

	/// <summary>
	/// A second release would retire whichever registration is outstanding — here, a source whose
	/// read is held — and report the target live over a source that has handed over nothing.
	/// </summary>
	[Fact]
	public async Task disposing_the_relay_twice_releases_the_target_once() {
		var stream = NewStream();
		AppendEvents(stream, Ones(2));
		using var readGate = new ManualResetEventSlim(false);
		var connection = new HookedConnection(_configured, beforeRead: s => {
			if (s == stream) { readGate.Wait(TestTimeouts.ThrottleWaitFor); }
		});
		var held = NewSource(connection: connection);
		var source = NewSource();
		var target = NewTarget();
		var relay = source.RelayTo(target);
		held.RelayTo(target);
		held.StartAsync(stream);
		var live = target.IsLive;

		relay.Dispose();
		relay.Dispose();
		// A probe queued behind both disposes: once it is handled, whatever they queued has been too.
		target.Publish(new RelayedChange(1, 0));
		AssertEx.IsOrBecomesTrue(() => target.ReceivedCount == 1, TestTimeouts.ThrottleWaitFor);

		Assert.False(live.IsCompleted);
		readGate.Set();
		await live.WaitAsync(TestTimeouts.ThrottleWaitFor);
	}

	[Fact]
	public async Task disposing_a_relay_stops_its_source_bounding_a_shared_stream() {
		var stream = NewStream();
		AppendEvents(stream, 1, 1, GatedValue, 1, 1, 1);
		using var handlerGate = new ManualResetEventSlim(false);
		var slow = NewSource(handlerGate);
		var fast = NewSource();
		var target = NewTarget();
		var slowRelay = slow.RelayTo(target);
		fast.RelayTo(target);

		slow.StartAsync(stream);
		fast.StartAsync(stream);
		var live = target.IsLive;

		try {
			Assert.True(slow.Parked.Wait(TestTimeouts.ThrottleWaitFor));
			AssertEx.IsOrBecomesTrue(() => target.ReceivedCount == 8, TestTimeouts.ThrottleWaitFor);
			Assert.Equal(1, Assert.Single(target.Applied).Version);

			slowRelay.Dispose();

			await live.WaitAsync(TestTimeouts.ThrottleWaitFor);
			AssertEx.IsOrBecomesTrue(() => Assert.Single(target.Applied).Version == 5, TestTimeouts.ThrottleWaitFor);
		} finally {
			handlerGate.Set();
		}

		AssertEx.IsOrBecomesTrue(() => slow.Count == 6 && slow.Idle, TestTimeouts.ThrottleWaitFor);
		Assert.Equal(8, target.ReceivedCount);
	}

	[Fact]
	public async Task a_stream_this_model_also_reads_is_reported_at_the_lesser_of_its_own_and_the_relays_position() {
		var stream = NewStream();
		AppendEvents(stream, 1, 1, GatedValue, 1, 1, 1);
		using var handlerGate = new ManualResetEventSlim(false);
		var slow = NewSource(handlerGate);
		var target = NewTarget();
		slow.RelayTo(target);

		slow.StartAsync(stream);
		target.StartAsync(stream);
		var live = target.IsLive;

		try {
			// The target has read all six itself and been told version 1 by the relay.
			Assert.True(slow.Parked.Wait(TestTimeouts.ThrottleWaitFor));
			AssertEx.IsOrBecomesTrue(() => target.OwnCount == 6 && target.ReceivedCount == 2, TestTimeouts.ThrottleWaitFor);
			Assert.Equal(1, Assert.Single(target.Applied).Version);
			Assert.False(live.IsCompleted);
		} finally {
			handlerGate.Set();
		}

		await live.WaitAsync(TestTimeouts.ThrottleWaitFor);
		Assert.Equal(5, Assert.Single(target.Applied).Version);
	}

	[Fact]
	public async Task a_start_that_fails_releases_the_target() {
		var stream = NewStream();
		var connection = new HookedConnection(_configured, beforeRead: s => {
			if (s == stream) { throw new IOException("store unreachable"); }
		});
		var source = NewSource(connection: connection);
		var target = NewTarget();
		source.RelayTo(target);
		var live = target.IsLive;
		Assert.False(live.IsCompleted);

		source.StartAsync(stream);

		await live.WaitAsync(TestTimeouts.ThrottleWaitFor);
		await Assert.ThrowsAsync<IOException>(() => source.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor));
		Assert.Empty(target.Applied);
	}

	[Fact]
	public async Task a_change_emitted_from_a_flush_carries_the_checkpoints_the_flush_was_given() {
		var stream = NewStream();
		AppendEvents(stream, Ones(3));
		var source = Track(new BufferedSourceModel("buffered-source", _configured));
		var target = NewTarget();
		source.RelayTo(target);

		source.StartAsync(stream);
		await target.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		// One flush, at the transition, so one change naming the whole history.
		var at = Assert.Single(Assert.Single(target.Seen));
		Assert.Equal(stream, at.StreamName);
		Assert.Equal(2, at.Version);
		Assert.Equal(3, Assert.Single(target.Received).Value);
	}

	[Fact]
	public void relaying_to_itself_is_refused() {
		var source = NewSource();

		Assert.Throws<ArgumentException>(() => source.RelayTo(source));
		Assert.True(source.IsLive.IsCompleted);
	}

	[Fact]
	public void relaying_that_would_close_a_cycle_is_refused() {
		var a = NewSource();
		var b = NewSource();
		var c = NewSource();
		a.RelayTo(b);
		b.RelayTo(c);

		Assert.Throws<ArgumentException>(() => b.RelayTo(a));
		Assert.Throws<ArgumentException>(() => c.RelayTo(a));

		Assert.True(a.IsLive.IsCompleted);
	}

	public void Dispose() {
		_disposables.ForEach(d => d.Dispose());
	}

	/// <summary>Folds a stream and raises one change per event, named by the count it has reached.</summary>
	private sealed class SourceModel : ReadModelBase, IHandle<RelayTestEvent> {
		private readonly ManualResetEventSlim? _handlerGate;

		public SourceModel(string name, IConfiguredConnection connection, ManualResetEventSlim? handlerGate = null)
			: base(name, connection) {
			_handlerGate = handlerGate;
			EventStream.Subscribe<RelayTestEvent>(this);
		}

		public int Count { get; private set; }
		public IReadOnlyList<StreamCheckpoint> Applied => AppliedCheckpoints;
		public readonly ManualResetEventSlim Parked = new(false);

		void IHandle<RelayTestEvent>.Handle(RelayTestEvent @event) {
			if (@event.Value == GatedValue) {
				Parked.Set();
				_handlerGate?.Wait(TimeSpan.FromMinutes(2));
			}
			Count++;
			Emit(new RelayedChange(@event.Value, Count));
		}

		public void EmitFromHere(IMessage change) => Emit(change);
		public Task EmitOnceLive(IMessage change) => OnceLive(() => Emit(change));

		/// <summary>The late-attach shape <see cref="ReadModelBase.RelayTo"/> prescribes: attach, then emit what the target lacks.</summary>
		public Task AttachOnceLive(TargetModel target, IMessage snapshot) =>
			OnceLive(() => {
				RelayTo(target);
				Emit(snapshot);
			});
	}

	/// <summary>A target that is also a source: re-raises each change it is handed.</summary>
	private sealed class ForwardingModel : ReadModelBase, IHandle<RelayedChange> {
		public ForwardingModel(string name, IConfiguredConnection connection) : base(name, connection) {
			EventStream.Subscribe<RelayedChange>(this);
		}

		void IHandle<RelayedChange>.Handle(RelayedChange change) => Emit(change);
	}

	/// <summary>Folds a stream into a buffer and raises one change per flush, named by the rows it held.</summary>
	private sealed class BufferedSourceModel : BufferedReadModelBase, IHandle<RelayTestEvent> {
		private readonly WriteBuffer<int, int> _rows;
		private int _count;

		public BufferedSourceModel(string name, IConfiguredConnection connection) : base(name, connection) {
			_rows = CreateBuffer<int, int>();
			EventStream.Subscribe<RelayTestEvent>(this);
		}

		void IHandle<RelayTestEvent>.Handle(RelayTestEvent @event) => _rows.Upsert(++_count, @event.Value);

		protected override void Flush(IReadOnlyList<StreamCheckpoint> checkpoints) =>
			Emit(new RelayedChange(_rows.Upserts.Count, checkpoints.Count));
	}

	/// <summary>Records each change it is handed and the checkpoints it was told at that moment.</summary>
	private sealed class TargetModel : ReadModelBase, IHandle<RelayedChange>, IHandle<RelayTestEvent> {
		private readonly ManualResetEventSlim? _handlerGate;
		private readonly List<RelayedChange> _received = [];
		private readonly List<IReadOnlyList<StreamCheckpoint>> _seen = [];

		public TargetModel(string name, IConfiguredConnection connection, ManualResetEventSlim? handlerGate = null)
			: base(name, connection) {
			_handlerGate = handlerGate;
			EventStream.Subscribe<RelayedChange>(this);
			EventStream.Subscribe<RelayTestEvent>(this);
		}

		public readonly ManualResetEventSlim Parked = new(false);

		/// <summary>Events folded from a stream this model reads itself, as against changes relayed to it.</summary>
		public int OwnCount { get; private set; }

		public int ReceivedCount {
			get {
				lock (ReaderLock) {
					return _received.Count;
				}
			}
		}

		public IReadOnlyList<RelayedChange> Received {
			get {
				lock (ReaderLock) {
					return _received.ToList();
				}
			}
		}

		public IReadOnlyList<IReadOnlyList<StreamCheckpoint>> Seen {
			get {
				lock (ReaderLock) {
					return _seen.ToList();
				}
			}
		}

		public IReadOnlyList<StreamCheckpoint> Applied => AppliedCheckpoints;

		public Task<(IReadOnlyList<StreamCheckpoint> Checkpoints, int Count)> Cut() =>
			ReadAtConsistentCut(checkpoints => (checkpoints, _received.Count));

		void IHandle<RelayedChange>.Handle(RelayedChange change) {
			if (change.Value == GatedValue) {
				Parked.Set();
				_handlerGate?.Wait(TimeSpan.FromMinutes(2));
			}
			_received.Add(change);
			_seen.Add(AppliedCheckpoints);
		}

		void IHandle<RelayTestEvent>.Handle(RelayTestEvent @event) => OwnCount++;
	}

	public record RelayTestEvent(int Value) : Event;

	/// <summary>What the source makes of an event; travels the relay only, never a stream.</summary>
	public record RelayedChange(int Value, int Ordinal) : Event;
}
