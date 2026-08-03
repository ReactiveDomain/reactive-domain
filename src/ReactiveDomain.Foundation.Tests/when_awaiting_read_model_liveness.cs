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
	public async Task does_not_complete_while_the_model_is_observably_incomplete() {
		var stream = NewStream();
		AppendEvents(stream, 3, 1);
		using var handlerGate = new ManualResetEventSlim(false);
		// Appending once the reader is done leaves an event only the listener can deliver — the
		// window an attach-time signal would return in, with the model still missing that event.
		var connection = new HookedConnection(_configured, afterRead: s => {
			if (s == stream) { AppendEvents(stream, 1, GatedValue); }
		});
		var rm = Track(new LivenessTestReadModel(connection, handlerGate));

		rm.StartAsync(stream);
		var live = rm.IsLive;

		// The reader's three events are folded, the fourth is parked inside a handler that has not
		// been let go, and the live marker is queued behind it — so this is structural, not a race.
		AssertEx.IsOrBecomesTrue(() => rm.Sum == 3, TestTimeouts.ThrottleWaitFor);
		Assert.False(live.IsCompleted);

		handlerGate.Set();
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

		public void Handle(LivenessTestEvent @event) {
			if (@event.Value == GatedValue) { _handlerGate?.Wait(TestTimeouts.ThrottleWaitFor); }
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
	private sealed class HookedConnection(
		IConfiguredConnection inner,
		Action<string>? beforeRead = null,
		Action<string>? afterRead = null) : IConfiguredConnection {
		public IStreamStoreConnection Connection => inner.Connection;
		public IStreamNameBuilder StreamNamer => inner.StreamNamer;
		public IEventSerializer Serializer => inner.Serializer;

		public IListener GetListener(string name) => inner.GetListener(name);
		public IListener GetQueuedListener(string name) => inner.GetQueuedListener(name);

		public IStreamReader GetReader(string name, Action<IMessage> handle) =>
			new HookedReader(inner.GetReader(name, handle), beforeRead, afterRead);

		public IRepository GetRepository(bool caching = false, Func<Guid>? currentPolicyUserId = null) =>
			inner.GetRepository(caching, currentPolicyUserId);

		public ICorrelatedRepository GetCorrelatedRepository(
			IRepository? baseRepository = null, bool caching = false, Func<Guid>? currentPolicyUserId = null) =>
			inner.GetCorrelatedRepository(baseRepository, caching, currentPolicyUserId);
	}

	private sealed class HookedReader(
		IStreamReader inner,
		Action<string>? beforeRead,
		Action<string>? afterRead) : IStreamReader {
		public long? Position => inner.Position;
		public string StreamName => inner.StreamName;
		public Action<IMessage> Handle { set => inner.Handle = value; }

		public bool Read(string stream, Func<bool> completionCheck, long? checkpoint = null, long? count = null,
			bool readBackwards = false) {
			beforeRead?.Invoke(stream);
			var read = inner.Read(stream, completionCheck, checkpoint, count, readBackwards);
			afterRead?.Invoke(stream);
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
