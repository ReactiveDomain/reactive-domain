using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests;

/// <summary>
/// Covers <see cref="ReadModelBase.AppliedCheckpoints"/>: it names what the handlers have run, where
/// <see cref="ReadModelBase.GetCheckpoint"/> names what the listeners have delivered.
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class when_tracking_applied_checkpoints : IClassFixture<StreamStoreConnectionFixture>, IDisposable {
	private const int GatedValue = 97;

	private readonly IStreamStoreConnection _conn;
	private readonly IConfiguredConnection _configured;
	private readonly IEventSerializer _serializer = new JsonMessageSerializer();
	private readonly IStreamNameBuilder _namer =
		new PrefixedCamelCaseStreamNameBuilder(nameof(when_tracking_applied_checkpoints));
	private readonly List<IDisposable> _disposables = [];

	public when_tracking_applied_checkpoints(StreamStoreConnectionFixture fixture) {
		_conn = fixture.Connection;
		_conn.Connect();
		_configured = new ConfiguredConnection(_conn, _namer, _serializer);
	}

	private string StreamFor(Guid id) => _namer.GenerateForAggregate(typeof(TestAggregate), id);

	private void AppendEvents(string streamName, int count, int value) {
		for (var i = 0; i < count; i++) {
			_conn.AppendToStream(streamName, ExpectedVersion.Any, null,
				_serializer.Serialize(new AppliedTestEvent(value)));
		}
	}

	private T Track<T>(T disposable) where T : IDisposable {
		_disposables.Add(disposable);
		return disposable;
	}

	[Fact]
	public async Task a_handler_is_told_the_event_it_is_applying_while_delivered_runs_ahead() {
		var stream = StreamFor(Guid.NewGuid());
		using var handlerGate = new ManualResetEventSlim(false);
		var rm = Track(new AppliedTestReadModel(_configured, handlerGate));
		rm.StartAsync(stream);
		await rm.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		// Five live events; the handler parks on the first, so four sit in the queue: delivered, not applied.
		AppendEvents(stream, 1, GatedValue);
		AppendEvents(stream, 4, 1);
		Assert.True(rm.Parked.Wait(TestTimeouts.ThrottleWaitFor));
		AssertEx.IsOrBecomesTrue(() => rm.GetCheckpoint().Single().Version == 4, TestTimeouts.ThrottleWaitFor);

		// Read inside the parked handler: AppliedCheckpoints takes ReaderLock, which the handler holds.
		Assert.Equal([0], rm.SeenInHandler);

		handlerGate.Set();
		AssertEx.IsOrBecomesTrue(() => rm.Count == 5, TestTimeouts.ThrottleWaitFor);
		Assert.Equal([0, 1, 2, 3, 4], rm.SeenInHandler);
		Assert.Equal(CheckpointOrder.Equal, StreamCheckpoint.Compare(rm.Applied, rm.GetCheckpoint()));
	}

	/// <summary>
	/// The read phase pairs like the live phase does, so a handler folding history is told the exact
	/// event it is on — not where the stream stood before the read.
	/// </summary>
	[Fact]
	public async Task a_handler_folding_history_is_told_the_event_it_is_applying() {
		var stream = StreamFor(Guid.NewGuid());
		AppendEvents(stream, 6, 1);
		var rm = Track(new AppliedTestReadModel(_configured));

		rm.StartAsync(stream);
		await rm.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		Assert.Equal([0, 1, 2, 3, 4, 5], rm.SeenInHandler);
	}

	[Fact]
	public void the_live_transition_marker_is_dispatched_but_not_counted() {
		var stream = StreamFor(Guid.NewGuid());
		AppendEvents(stream, 3, 1);
		var rm = Track(new AppliedTestReadModel(_configured));

		rm.Start(stream, blockUntilLive: true);

		AssertEx.IsOrBecomesTrue(() => rm.Count == 3, TestTimeouts.ThrottleWaitFor);
		Assert.Equal(3, rm.Version);
	}

	[Fact]
	public async Task once_the_read_drains_the_stream_is_reported_where_the_read_left_it() {
		var stream = StreamFor(Guid.NewGuid());
		AppendEvents(stream, 6, 1);
		var rm = Track(new AppliedTestReadModel(_configured));

		rm.StartAsync(stream);
		await rm.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		var applied = Assert.Single(rm.Applied);
		Assert.Equal(stream, applied.StreamName);
		Assert.Equal(5, applied.Version);
		Assert.Equal(CheckpointOrder.Equal, StreamCheckpoint.Compare(rm.Applied, rm.GetCheckpoint()));
	}

	[Fact]
	public async Task a_stream_that_has_delivered_nothing_reports_a_null_version() {
		var stream = StreamFor(Guid.NewGuid());
		var rm = Track(new AppliedTestReadModel(_configured));

		rm.StartAsync(stream);
		await rm.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		var applied = Assert.Single(rm.Applied);
		Assert.Equal(stream, applied.StreamName);
		Assert.Null(applied.Version);
	}

	/// <summary>
	/// The typed overloads resume from the checkpoint like the named one does. A resume that finds no
	/// newer events must leave the listener at the checkpoint, not at the beginning of the stream.
	/// </summary>
	[Fact]
	public async Task resuming_a_typed_stream_with_no_new_events_redelivers_nothing() {
		var id = Guid.NewGuid();
		var stream = StreamFor(id);
		AppendEvents(stream, 4, 1);
		var rm = Track(new AppliedTestReadModel(_configured));

		rm.StartAsync<TestAggregate>(id, checkpoint: 3);
		await rm.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);
		Assert.Equal(3, rm.Applied.Single().Version);

		AppendEvents(stream, 1, 5);
		AssertEx.IsOrBecomesTrue(() => rm.Count == 1, TestTimeouts.ThrottleWaitFor);

		Assert.Equal(5, rm.Sum); // nothing before the checkpoint was delivered again
		Assert.Equal(4, rm.Applied.Single().Version);
	}

	public void Dispose() {
		_disposables.ForEach(d => d.Dispose());
	}

	private sealed class AppliedTestReadModel : ReadModelBase, IHandle<AppliedTestEvent> {
		private readonly ManualResetEventSlim? _handlerGate;

		public AppliedTestReadModel(IConfiguredConnection connection, ManualResetEventSlim? handlerGate = null)
			: base(nameof(AppliedTestReadModel), connection) {
			_handlerGate = handlerGate;
			// ReSharper disable once RedundantTypeArgumentsOfMethod
			EventStream.Subscribe<AppliedTestEvent>(this);
		}

		public long Sum { get; private set; }
		public int Count { get; private set; }
		public List<long?> SeenInHandler { get; } = [];
		public IReadOnlyList<StreamCheckpoint> Applied => AppliedCheckpoints;
		public readonly ManualResetEventSlim Parked = new(false);

		public void Handle(AppliedTestEvent @event) {
			SeenInHandler.Add(AppliedCheckpoints.Single().Version);
			if (@event.Value == GatedValue) {
				Parked.Set();
				_handlerGate?.Wait(TimeSpan.FromMinutes(2));
			}
			Sum += @event.Value;
			Count++;
		}
	}

	public record AppliedTestEvent(int Value) : Event;
}
