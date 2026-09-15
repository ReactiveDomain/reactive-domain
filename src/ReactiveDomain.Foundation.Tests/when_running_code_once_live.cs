using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests;

/// <summary>
/// Covers <see cref="ReadModelBase.OnceLive"/>: the callback runs at the live transition, on the
/// queue thread under the reader lock, sequenced with the handlers — and its task carries its fate.
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class when_running_code_once_live : IClassFixture<StreamStoreConnectionFixture>, IDisposable {
	private const int GatedValue = 97;

	private readonly IStreamStoreConnection _conn;
	private readonly IConfiguredConnection _configured;
	private readonly IEventSerializer _serializer = new JsonMessageSerializer();
	private readonly IStreamNameBuilder _namer =
		new PrefixedCamelCaseStreamNameBuilder(nameof(when_running_code_once_live));
	private readonly List<IDisposable> _disposables = [];

	public when_running_code_once_live(StreamStoreConnectionFixture fixture) {
		_conn = fixture.Connection;
		_conn.Connect();
		_configured = new ConfiguredConnection(_conn, _namer, _serializer);
	}

	private string NewStream() => _namer.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());

	private void AppendEvents(string streamName, int count, int value) {
		for (var i = 0; i < count; i++) {
			_conn.AppendToStream(streamName, ExpectedVersion.Any, null,
				_serializer.Serialize(new OnceLiveTestEvent(value)));
		}
	}

	private T Track<T>(T disposable) where T : IDisposable {
		_disposables.Add(disposable);
		return disposable;
	}

	[Fact]
	public async Task runs_with_history_folded_on_the_queue_thread_under_the_reader_lock() {
		var stream = NewStream();
		AppendEvents(stream, 5, 1);
		var rm = Track(new OnceLiveTestReadModel(_configured));

		rm.StartAsync(stream);
		long seenSum = -1;
		int? seenThread = null;
		var lockHeld = false;
		var once = rm.OnceLive(() => {
			seenSum = rm.Sum;
			seenThread = Environment.CurrentManagedThreadId;
			lockHeld = rm.HoldsReaderLock;
		});

		await once.WaitAsync(TestTimeouts.ThrottleWaitFor);
		Assert.Equal(5, seenSum);
		Assert.Equal(rm.HandlerThread, seenThread);
		Assert.True(lockHeld);
	}

	/// <summary>
	/// The property an <c>IsLive</c> continuation cannot offer: nothing queued behind the history
	/// has been handled when the callback runs.
	/// </summary>
	[Fact]
	public async Task runs_before_anything_queued_behind_the_history_is_handled() {
		var stream = NewStream();
		AppendEvents(stream, 3, 1);
		using var handlerGate = new ManualResetEventSlim(false);
		// Queued once the read is done and ahead of the sentinel, so it is history; parking on it
		// holds the sentinel in the queue. The synchronous Start returns only once the sentinel is
		// queued, so everything handed to the model after it returns is behind the sentinel.
		var connection = new HookedConnection(_configured, afterRead: (s, enqueue) => {
			if (s == stream) { enqueue(new OnceLiveTestEvent(GatedValue)); }
		});
		var rm = Track(new OnceLiveTestReadModel(connection, handlerGate));

		rm.Start(stream);
		Assert.True(rm.Parked.Wait(TestTimeouts.ThrottleWaitFor));
		for (var i = 0; i < 3; i++) {
			rm.Publish(new OnceLiveTestEvent(10));
		}
		var seenCount = -1;
		var once = rm.OnceLive(() => seenCount = rm.Count);
		Assert.False(once.IsCompleted);

		handlerGate.Set();
		await once.WaitAsync(TestTimeouts.ThrottleWaitFor);

		Assert.Equal(4, seenCount); // the three read, the one parked; none of the three behind
		AssertEx.IsOrBecomesTrue(() => rm.Count == 7, TestTimeouts.ThrottleWaitFor);
	}

	[Fact]
	public async Task registered_while_live_runs_behind_what_is_already_queued() {
		var stream = NewStream();
		AppendEvents(stream, 2, 1);
		using var handlerGate = new ManualResetEventSlim(false);
		var rm = Track(new OnceLiveTestReadModel(_configured, handlerGate));
		rm.StartAsync(stream);
		await rm.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		// Park the queue on the first of three, then register: the callback goes behind all three.
		rm.Publish(new OnceLiveTestEvent(GatedValue));
		rm.Publish(new OnceLiveTestEvent(1));
		rm.Publish(new OnceLiveTestEvent(1));
		Assert.True(rm.Parked.Wait(TestTimeouts.ThrottleWaitFor));
		var seenCount = -1;
		var once = rm.OnceLive(() => seenCount = rm.Count);
		Assert.False(once.IsCompleted);

		handlerGate.Set();
		await once.WaitAsync(TestTimeouts.ThrottleWaitFor);

		Assert.Equal(5, seenCount);
	}

	[Fact]
	public async Task a_throwing_callback_faults_its_own_task_and_nothing_else() {
		var stream = NewStream();
		AppendEvents(stream, 2, 1);
		var rm = Track(new OnceLiveTestReadModel(_configured));

		rm.StartAsync(stream);
		var once = rm.OnceLive(() => throw new InvalidOperationException("flush failed"));
		var live = rm.IsLive;

		var thrown = await Assert.ThrowsAsync<InvalidOperationException>(() => once.WaitAsync(TestTimeouts.ThrottleWaitFor));
		Assert.Equal("flush failed", thrown.Message);
		await live.WaitAsync(TestTimeouts.ThrottleWaitFor);

		// The model is unharmed.
		rm.Publish(new OnceLiveTestEvent(5));
		AssertEx.IsOrBecomesTrue(() => rm.Sum == 7, TestTimeouts.ThrottleWaitFor);
	}

	[Fact]
	public async Task is_cancelled_when_the_model_is_disposed_before_the_transition() {
		var stream = NewStream();
		AppendEvents(stream, 2, 1);
		using var readGate = new ManualResetEventSlim(false);
		var connection = new HookedConnection(_configured, beforeRead: s => {
			if (s == stream) { readGate.Wait(TestTimeouts.ThrottleWaitFor); }
		});
		var rm = new OnceLiveTestReadModel(connection);

		rm.StartAsync(stream);
		var once = rm.OnceLive(() => { });
		Assert.False(once.IsCompleted);

		rm.Dispose();
		readGate.Set();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => once.WaitAsync(TestTimeouts.ThrottleWaitFor));
	}

	/// <summary>
	/// Registered after the dispose has retired the outstanding start, so there is no transition
	/// left to wait for and nothing else to abandon it.
	/// </summary>
	[Fact]
	public async Task is_cancelled_when_registered_after_dispose_with_a_start_outstanding() {
		var stream = NewStream();
		AppendEvents(stream, 2, 1);
		using var readGate = new ManualResetEventSlim(false);
		var connection = new HookedConnection(_configured, beforeRead: s => {
			if (s == stream) { readGate.Wait(TestTimeouts.ThrottleWaitFor); }
		});
		var rm = new OnceLiveTestReadModel(connection);
		rm.StartAsync(stream);
		rm.Dispose();

		var once = rm.OnceLive(() => { });
		readGate.Set();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => once.WaitAsync(TestTimeouts.ThrottleWaitFor));
	}

	[Fact]
	public async Task faults_with_the_start_failure_when_a_read_fails_before_the_transition() {
		var stream = NewStream();
		using var readGate = new ManualResetEventSlim(false);
		var connection = new HookedConnection(_configured, beforeRead: s => {
			if (s != stream) { return; }
			readGate.Wait(TestTimeouts.ThrottleWaitFor);
			throw new IOException("store unreachable");
		});
		var rm = Track(new OnceLiveTestReadModel(connection));

		rm.StartAsync(stream);
		var once = rm.OnceLive(() => { });
		readGate.Set();

		await Assert.ThrowsAsync<IOException>(() => once.WaitAsync(TestTimeouts.ThrottleWaitFor));
	}

	[Fact]
	public async Task runs_once_and_a_later_start_does_not_run_it_again() {
		var stream1 = NewStream();
		var stream2 = NewStream();
		AppendEvents(stream1, 2, 1);
		AppendEvents(stream2, 3, 1);
		var rm = Track(new OnceLiveTestReadModel(_configured));
		var runs = 0;

		rm.StartAsync(stream1);
		await rm.OnceLive(() => runs++).WaitAsync(TestTimeouts.ThrottleWaitFor);
		rm.StartAsync(stream2);
		await rm.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		Assert.Equal(1, runs);
		Assert.Equal(5, rm.Sum);
	}

	public void Dispose() {
		_disposables.ForEach(d => d.Dispose());
	}

	private sealed class OnceLiveTestReadModel : ReadModelBase, IHandle<OnceLiveTestEvent> {
		private readonly ManualResetEventSlim? _handlerGate;

		public OnceLiveTestReadModel(IConfiguredConnection connection, ManualResetEventSlim? handlerGate = null)
			: base(nameof(OnceLiveTestReadModel), connection) {
			_handlerGate = handlerGate;
			// ReSharper disable once RedundantTypeArgumentsOfMethod
			EventStream.Subscribe<OnceLiveTestEvent>(this);
		}

		public long Sum { get; private set; }
		public int Count { get; private set; }
		public int? HandlerThread { get; private set; }
		public bool HoldsReaderLock => Monitor.IsEntered(ReaderLock);
		public readonly ManualResetEventSlim Parked = new(false);

		public void Handle(OnceLiveTestEvent @event) {
			HandlerThread = Environment.CurrentManagedThreadId;
			if (@event.Value == GatedValue) {
				Parked.Set();
				_handlerGate?.Wait(TimeSpan.FromMinutes(2));
			}
			Sum += @event.Value;
			Count++;
		}
	}

	public record OnceLiveTestEvent(int Value) : Event;
}
