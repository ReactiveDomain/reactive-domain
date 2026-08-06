using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests;

/// <summary>
/// Covers <see cref="ReadModelBase.IsLive"/>: the drained signal — it completes only once every
/// started stream has dispatched its history through the model's handlers.
/// </summary>
/// <remarks>
/// The waits here are structural, not timing based. A read gate injected into the reader and a
/// handler gate inside the model hold a stream open for as long as the test needs, so
/// "not live yet" is a fact about the pipeline rather than a race the test hopes to win.
/// </remarks>
// ReSharper disable once InconsistentNaming
public sealed class when_awaiting_read_model_liveness : IClassFixture<StreamStoreConnectionFixture>, IDisposable {
	private const int GatedValue = 97;

	private readonly IStreamStoreConnection _conn;
	private readonly IConfiguredConnection _configured;
	private readonly IEventSerializer _serializer = new JsonMessageSerializer();
	private readonly IStreamNameBuilder _namer =
		new PrefixedCamelCaseStreamNameBuilder(nameof(when_awaiting_read_model_liveness));
	private readonly List<IDisposable> _disposables = [];

	public when_awaiting_read_model_liveness(StreamStoreConnectionFixture fixture) {
		_conn = fixture.Connection;
		_conn.Connect();
		_configured = new ConfiguredConnection(_conn, _namer, _serializer);
	}

	private string NewStream() => _namer.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());

	private void AppendEvents(string streamName, int count, int value) {
		for (var i = 0; i < count; i++) {
			_conn.AppendToStream(streamName, ExpectedVersion.Any, null,
				_serializer.Serialize(new LivenessTestEvent(i, value)));
		}
	}

	private T Track<T>(T disposable) where T : IDisposable {
		_disposables.Add(disposable);
		return disposable;
	}

	[Fact]
	public async Task does_not_complete_while_the_read_is_still_folding() {
		var stream = NewStream();
		AppendEvents(stream, 3, 1);
		AppendEvents(stream, 1, GatedValue);
		using var handlerGate = new ManualResetEventSlim(false);
		var rm = Track(new LivenessTestReadModel(_configured, handlerGate));

		rm.StartAsync(stream);
		var live = rm.IsLive;

		// Three folded and the fourth parked inside its handler, so the read cannot see an idle
		// queue and cannot return. Structural, not a race.
		// Released in a finally: a parked handler outlives a failed assertion, and disposal then
		// reports a stuck queue thread over the top of the assertion that actually failed.
		try {
			Assert.True(rm.Parked.Wait(TestTimeouts.ThrottleWaitFor));
			Assert.Equal(3, rm.Sum);
			Assert.False(live.IsCompleted);
		} finally {
			handlerGate.Set();
		}

		await live.WaitAsync(TestTimeouts.ThrottleWaitFor);
		Assert.Equal(3 + GatedValue, rm.Sum);
	}

	/// <summary>
	/// The read phase is the gate, so an event appended after it is live traffic. Waiting for it
	/// would make liveness depend on the subscription rather than on the history being folded.
	/// </summary>
	[Fact]
	public async Task does_not_wait_for_events_appended_after_the_read() {
		var stream = NewStream();
		AppendEvents(stream, 3, 1);
		// Appended once the reader is done, so only the listener can ever deliver it.
		var connection = new HookedConnection(_configured, afterRead: (s, _) => {
			if (s == stream) { AppendEvents(stream, 1, GatedValue); }
		});
		var rm = Track(new LivenessTestReadModel(connection));

		rm.StartAsync(stream);
		var live = rm.IsLive;

		// Completes on the read alone. The appended event may or may not have arrived by now - that
		// it is not waited for is the point, so its arrival is not part of this assertion.
		await live.WaitAsync(TestTimeouts.ThrottleWaitFor);
		Assert.True(rm.Sum >= 3);

		// It does still arrive, through the subscription.
		AssertEx.IsOrBecomesTrue(() => rm.Sum == 3 + GatedValue, TestTimeouts.ThrottleWaitFor);
	}

	/// <summary>
	/// Liveness does not depend on the subscription, so nothing else here would notice if the
	/// subscription stopped delivering once the read phase was over.
	/// </summary>
	[Fact]
	public async Task events_appended_after_going_live_arrive_through_the_subscription() {
		var stream = NewStream();
		AppendEvents(stream, 3, 1);
		var rm = Track(new LivenessTestReadModel(_configured));

		rm.StartAsync(stream);
		await rm.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		// Nothing has been appended since the read, so this is the read phase's fold exactly.
		Assert.Equal(3, rm.Sum);
		Assert.Equal(3, rm.Count);

		// Appended after the model went live, so only the listener can deliver them.
		AppendEvents(stream, 2, 10);

		AssertEx.IsOrBecomesTrue(() => rm.Count == 5, TestTimeouts.ThrottleWaitFor);
		Assert.Equal(23, rm.Sum);
	}

	/// <summary>
	/// The ordering the signal rests on: it is queued behind what the read delivered, so it cannot be
	/// dequeued until those events have run through the handlers.
	/// </summary>
	/// <remarks>
	/// The synchronous <c>Start</c> is load-bearing, not a stylistic choice. It returns only once the
	/// start path has queued the signal, so a task already completed on return proves the signal
	/// queued ahead of a parked event rather than behind it. Under <c>StartAsync</c> the assertion
	/// would instead race the start thread, which is enough for a model that retires the stream the
	/// moment the read returns to pass on a runtime where that race is habitually lost.
	/// </remarks>
	[Fact]
	public async Task the_signal_is_queued_behind_the_events_the_read_delivered() {
		var stream = NewStream();
		AppendEvents(stream, 3, 1);
		using var handlerGate = new ManualResetEventSlim(false);
		// Queued once the reader has finished the stream, so nothing but its position relative to the
		// signal decides whether it is folded before the model reports live.
		var connection = new HookedConnection(_configured, afterRead: (s, enqueue) => {
			if (s == stream) { enqueue(new LivenessTestEvent(3, GatedValue)); }
		});
		var rm = Track(new LivenessTestReadModel(connection, handlerGate));

		rm.Start(stream);
		var live = rm.IsLive;

		try {
			Assert.True(rm.Parked.Wait(TestTimeouts.ThrottleWaitFor));
			Assert.False(live.IsCompleted);
		} finally {
			handlerGate.Set();
		}

		await live.WaitAsync(TestTimeouts.ThrottleWaitFor);
		Assert.Equal(3 + GatedValue, rm.Sum);
	}

	[Fact]
	public async Task completes_with_state_fully_folded() {
		var stream = NewStream();
		AppendEvents(stream, 10, 2);
		var rm = Track(new LivenessTestReadModel(_configured));

		rm.StartAsync(stream);
		await rm.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		// Asserted directly, with no polling: the barrier is the whole claim under test.
		Assert.Equal(10, rm.Count);
		Assert.Equal(20, rm.Sum);
	}

	[Fact]
	public async Task completes_for_a_synchronously_started_stream() {
		var stream = NewStream();
		AppendEvents(stream, 4, 3);
		var rm = Track(new LivenessTestReadModel(_configured));

		// The synchronous overload is covered too — it registers before it reads, like StartAsync.
		rm.Start(stream);
		await rm.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		Assert.Equal(12, rm.Sum);
	}

	[Fact]
	public async Task completes_only_when_every_started_stream_has_drained() {
		var stream1 = NewStream();
		var stream2 = NewStream();
		AppendEvents(stream1, 5, 2);
		AppendEvents(stream2, 5, 4);
		using var readGate = new ManualResetEventSlim(false);
		var connection = new HookedConnection(_configured, beforeRead: s => {
			if (s == stream2) { readGate.Wait(TestTimeouts.ThrottleWaitFor); }
		});
		var rm = Track(new LivenessTestReadModel(connection));

		rm.StartAsync(stream1);
		rm.StartAsync(stream2);
		var live = rm.IsLive;

		// The first stream drains fully while the second has not been read at all.
		AssertEx.IsOrBecomesTrue(() => rm.Sum == 10, TestTimeouts.ThrottleWaitFor);
		Assert.False(live.IsCompleted);

		readGate.Set();
		await live.WaitAsync(TestTimeouts.ThrottleWaitFor);
		Assert.Equal(30, rm.Sum);
	}

	[Fact]
	public async Task a_later_start_re_arms_the_signal() {
		var stream1 = NewStream();
		var stream2 = NewStream();
		AppendEvents(stream1, 5, 2);
		AppendEvents(stream2, 5, 4);
		using var readGate = new ManualResetEventSlim(false);
		var connection = new HookedConnection(_configured, beforeRead: s => {
			if (s == stream2) { readGate.Wait(TestTimeouts.ThrottleWaitFor); }
		});
		var rm = Track(new LivenessTestReadModel(connection));

		rm.StartAsync(stream1);
		var first = rm.IsLive;
		await first.WaitAsync(TestTimeouts.ThrottleWaitFor);
		Assert.Equal(10, rm.Sum);

		rm.StartAsync(stream2);
		var second = rm.IsLive;
		Assert.NotSame(first, second);
		Assert.True(first.IsCompleted); // the task already handed out stays completed
		Assert.False(second.IsCompleted);

		readGate.Set();
		await second.WaitAsync(TestTimeouts.ThrottleWaitFor);
		Assert.Equal(30, rm.Sum);
	}

	[Fact]
	public async Task covers_the_streams_a_snapshot_read_model_restores() {
		var stream = NewStream();
		AppendEvents(stream, 6, 5);
		var rm = Track(new LivenessTestSnapshotReadModel(_configured,
			new ReadModelState(nameof(LivenessTestSnapshotReadModel), [new Tuple<string, long>(stream, 2)], new object())));

		await rm.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		Assert.Equal(15, rm.Sum); // events 3..5 only, the checkpoint was 2
	}

	public void Dispose() {
		_disposables.ForEach(d => d.Dispose());
	}

	private sealed class LivenessTestReadModel : ReadModelBase, IHandle<LivenessTestEvent> {
		private readonly ManualResetEventSlim? _handlerGate;

		public LivenessTestReadModel(IConfiguredConnection connection, ManualResetEventSlim? handlerGate = null)
			: base(nameof(LivenessTestReadModel), connection) {
			_handlerGate = handlerGate;
			// ReSharper disable once RedundantTypeArgumentsOfMethod
			EventStream.Subscribe<LivenessTestEvent>(this);
		}

		public long Sum { get; private set; }
		public int Count { get; private set; }

		/// <summary>Set when a handler parks on the gate, so a test waits for that instead of polling.</summary>
		public readonly ManualResetEventSlim Parked = new(false);

		public void Handle(LivenessTestEvent @event) {
			if (@event.Value == GatedValue) {
				Parked.Set();
				// Long, not unbounded: a test that fails before releasing the gate must not leave the
				// queue thread parked, or Dispose reports a stuck thread instead of the real failure.
				_handlerGate?.Wait(TimeSpan.FromMinutes(2));
			}
			Sum += @event.Value;
			Count++;
		}
	}

	private sealed class LivenessTestSnapshotReadModel : SnapshotReadModel, IHandle<LivenessTestEvent> {
		public LivenessTestSnapshotReadModel(IConfiguredConnection connection, ReadModelState snapshot)
			: base(nameof(LivenessTestSnapshotReadModel), connection) {
			// ReSharper disable once RedundantTypeArgumentsOfMethod
			EventStream.Subscribe<LivenessTestEvent>(this);
			Restore(snapshot);
		}

		public long Sum { get; private set; }

		public void Handle(LivenessTestEvent @event) => Sum += @event.Value;

		protected override void ApplyState(ReadModelState snapshot) { }

		public override ReadModelState GetState() =>
			new(nameof(LivenessTestSnapshotReadModel), GetCheckpoint(), new object());
	}

	/// <summary>
	/// An <see cref="IConfiguredConnection"/> whose readers call back before and after reading a
	/// named stream, giving tests a deterministic hold point inside <c>ReadModelBase.Start</c>'s
	/// read-then-subscribe sequence. Everything else passes straight through.
	/// </summary>
	/// <remarks>
	/// <paramref name="afterRead"/> is handed the model's own queue-enqueue delegate, so a test can
	/// place a message where the read would have left one: queued, unhandled, and ahead of whatever
	/// the start path queues next.
	/// </remarks>
	private sealed class HookedConnection(
		IConfiguredConnection inner,
		Action<string>? beforeRead = null,
		Action<string, Action<IMessage>>? afterRead = null) : IConfiguredConnection {
		public IStreamStoreConnection Connection => inner.Connection;
		public IStreamNameBuilder StreamNamer => inner.StreamNamer;
		public IEventSerializer Serializer => inner.Serializer;

		public IListener GetListener(string name) => inner.GetListener(name);
		public IListener GetQueuedListener(string name) => inner.GetQueuedListener(name);

		public IStreamReader GetReader(string name, Action<IMessage> handle) =>
			new HookedReader(inner.GetReader(name, handle), handle, beforeRead, afterRead);

		public IRepository GetRepository(bool caching = false, Func<Guid>? currentPolicyUserId = null) =>
			inner.GetRepository(caching, currentPolicyUserId);

		public ICorrelatedRepository GetCorrelatedRepository(
			IRepository? baseRepository = null, bool caching = false, Func<Guid>? currentPolicyUserId = null) =>
			inner.GetCorrelatedRepository(baseRepository, caching, currentPolicyUserId);
	}

	private sealed class HookedReader(
		IStreamReader inner,
		Action<IMessage> handle,
		Action<string>? beforeRead,
		Action<string, Action<IMessage>>? afterRead) : IStreamReader {
		public long? Position => inner.Position;
		public string StreamName => inner.StreamName;
		public Action<IMessage> Handle { set => inner.Handle = value; }

		public bool Read(string stream, Func<bool> completionCheck, long? checkpoint = null, long? count = null,
			bool readBackwards = false) {
			beforeRead?.Invoke(stream);
			var read = inner.Read(stream, completionCheck, checkpoint, count, readBackwards);
			afterRead?.Invoke(stream, handle);
			return read;
		}

		public bool Read(Type tMessage, Func<bool> completionCheck, long? checkpoint = null, long? count = null,
			bool readBackwards = false) => inner.Read(tMessage, completionCheck, checkpoint, count, readBackwards);

		public bool Read<TAggregate>(Guid id, Func<bool> completionCheck, long? checkpoint = null, long? count = null,
			bool readBackwards = false) where TAggregate : class, IEventSource =>
			inner.Read<TAggregate>(id, completionCheck, checkpoint, count, readBackwards);

		public bool Read<TAggregate>(Func<bool> completionCheck, long? checkpoint = null, long? count = null,
			bool readBackwards = false) where TAggregate : class, IEventSource =>
			inner.Read<TAggregate>(completionCheck, checkpoint, count, readBackwards);

		public void Cancel() => inner.Cancel();
		public void Dispose() => inner.Dispose();
	}

	public record LivenessTestEvent(int Number, int Value) : Event;
}
