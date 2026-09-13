using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests;

/// <summary>
/// Covers <see cref="ReadModelBase.StartAllAsync"/>: the set is checked before anything starts, and
/// the returned task is read after every stream has registered, so it covers all of them.
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class when_starting_all_streams : IClassFixture<StreamStoreConnectionFixture>, IDisposable {
	private readonly IStreamStoreConnection _conn;
	private readonly IConfiguredConnection _configured;
	private readonly IEventSerializer _serializer = new JsonMessageSerializer();
	private readonly IStreamNameBuilder _namer =
		new PrefixedCamelCaseStreamNameBuilder(nameof(when_starting_all_streams));
	private readonly List<IDisposable> _disposables = [];

	public when_starting_all_streams(StreamStoreConnectionFixture fixture) {
		_conn = fixture.Connection;
		_conn.Connect();
		_configured = new ConfiguredConnection(_conn, _namer, _serializer);
	}

	private string NewStream() => _namer.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());

	private void AppendEvents(string streamName, int count, int value) {
		for (var i = 0; i < count; i++) {
			_conn.AppendToStream(streamName, ExpectedVersion.Any, null,
				_serializer.Serialize(new StartAllTestEvent(value)));
		}
	}

	private T Track<T>(T disposable) where T : IDisposable {
		_disposables.Add(disposable);
		return disposable;
	}

	/// <summary>
	/// The case the ordering rule exists for: the first stream drains long before the second, so an
	/// <c>IsLive</c> read part-way through would be a completed task covering one stream.
	/// </summary>
	[Fact]
	public async Task the_task_covers_every_stream_when_the_first_drains_first() {
		var stream1 = NewStream();
		var stream2 = NewStream();
		AppendEvents(stream1, 5, 2);
		AppendEvents(stream2, 5, 4);
		using var readGate = new ManualResetEventSlim(false);
		var connection = new HookedConnection(_configured, beforeRead: s => {
			if (s == stream2) { readGate.Wait(TestTimeouts.ThrottleWaitFor); }
		});
		var rm = Track(new StartAllTestReadModel(connection));

		var all = rm.StartAllAsync([StreamStarter.Named(stream1), StreamStarter.Named(stream2)]);

		Assert.False(all.IsCompleted);
		readGate.Set();
		await all.WaitAsync(TestTimeouts.ThrottleWaitFor);
		Assert.Equal(30, rm.Sum);
	}

	[Fact]
	public void starting_nothing_is_refused() {
		var rm = Track(new StartAllTestReadModel(_configured));

		// Synchronous: the set is refused, rather than a task faulting later.
		var thrown = Assert.Throws<ArgumentException>(() => { _ = rm.StartAllAsync([]); });

		Assert.Contains("at least one", thrown.Message);
	}

	[Fact]
	public void one_stream_named_twice_is_refused() {
		var stream = NewStream();
		var rm = Track(new StartAllTestReadModel(_configured));

		var thrown = Assert.Throws<ArgumentException>(
			() => { _ = rm.StartAllAsync([StreamStarter.Named(stream), StreamStarter.Named(stream)]); });

		Assert.Contains("appears twice", thrown.Message);
	}

	[Fact]
	public async Task a_stream_already_started_is_refused() {
		var stream1 = NewStream();
		var stream2 = NewStream();
		AppendEvents(stream1, 5, 2);
		var rm = Track(new StartAllTestReadModel(_configured));
		await rm.StartAllAsync([StreamStarter.Named(stream1)]).WaitAsync(TestTimeouts.ThrottleWaitFor);

		var thrown = Assert.Throws<InvalidOperationException>(
			() => { _ = rm.StartAllAsync([StreamStarter.Named(stream2), StreamStarter.Named(stream1)]); });

		Assert.Contains("already listening", thrown.Message);
	}

	/// <summary>
	/// The reason the whole set is resolved before any of it starts: a refusal must not leave the
	/// model holding half of what was asked for.
	/// </summary>
	[Fact]
	public void a_refused_set_starts_nothing() {
		var stream1 = NewStream();
		var stream2 = NewStream();
		AppendEvents(stream1, 5, 2);
		var rm = Track(new StartAllTestReadModel(_configured));

		Assert.Throws<ArgumentException>(
			() => { _ = rm.StartAllAsync([StreamStarter.Named(stream1), StreamStarter.Named(stream2), StreamStarter.Named(stream1)]); });

		Assert.Empty(rm.GetCheckpoint());
		Assert.Equal(0, rm.Sum);
	}

	[Fact]
	public async Task the_callback_runs_after_every_stream_has_drained() {
		var stream1 = NewStream();
		var stream2 = NewStream();
		AppendEvents(stream1, 5, 2);
		AppendEvents(stream2, 5, 4);
		using var readGate = new ManualResetEventSlim(false);
		var connection = new HookedConnection(_configured, beforeRead: s => {
			if (s == stream2) { readGate.Wait(TestTimeouts.ThrottleWaitFor); }
		});
		var rm = Track(new StartAllTestReadModel(connection));
		long seen = -1;

		var all = rm.StartAllAsync(
			[StreamStarter.Named(stream1), StreamStarter.Named(stream2)],
			onceLive: () => seen = rm.Sum);
		Assert.False(all.IsCompleted);

		readGate.Set();
		await all.WaitAsync(TestTimeouts.ThrottleWaitFor);
		Assert.Equal(30, seen);
	}

	/// <summary>
	/// Ungated, so every stream can drain while the call is still registering. The callback still runs
	/// at the transition with everything folded, rather than behind it.
	/// </summary>
	[Fact]
	public async Task the_callback_is_registered_before_the_transition_can_happen() {
		var stream1 = NewStream();
		var stream2 = NewStream();
		AppendEvents(stream1, 5, 2);
		AppendEvents(stream2, 5, 4);
		var rm = Track(new StartAllTestReadModel(_configured));
		long seen = -1;

		await rm.StartAllAsync(
			[StreamStarter.Named(stream1), StreamStarter.Named(stream2)],
			onceLive: () => seen = rm.Sum).WaitAsync(TestTimeouts.ThrottleWaitFor);

		Assert.Equal(30, seen);
	}

	/// <summary>
	/// Every stream starts on a task pool thread, so a read that fails faults the returned task rather
	/// than throwing out of the call — unlike a refused set, which is rejected synchronously.
	/// </summary>
	[Fact]
	public async Task a_start_that_fails_faults_the_task() {
		var stream = NewStream();
		var connection = new HookedConnection(_configured, beforeRead: s => {
			if (s == stream) { throw new IOException("store unreachable"); }
		});
		var rm = Track(new StartAllTestReadModel(connection));

		var all = rm.StartAllAsync([StreamStarter.Named(stream)]);

		await Assert.ThrowsAsync<IOException>(() => all.WaitAsync(TestTimeouts.ThrottleWaitFor));
	}

	public void Dispose() {
		_disposables.ForEach(d => d.Dispose());
	}

	private sealed class StartAllTestReadModel : ReadModelBase, IHandle<StartAllTestEvent> {
		public StartAllTestReadModel(IConfiguredConnection connection) : base(nameof(StartAllTestReadModel), connection) {
			// ReSharper disable once RedundantTypeArgumentsOfMethod
			EventStream.Subscribe<StartAllTestEvent>(this);
		}

		public long Sum { get; private set; }

		public void Handle(StartAllTestEvent @event) => Sum += @event.Value;
	}

	public record StartAllTestEvent(int Value) : Event;
}
