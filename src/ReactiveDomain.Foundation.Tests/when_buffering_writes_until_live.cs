using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests;

/// <summary>
/// Covers <see cref="BufferedReadModelBase"/>: no writes during catch-up, one flush at the live
/// transition, then one per event — each handed the checkpoints of exactly what it covers.
/// </summary>
// ReSharper disable once InconsistentNaming
public sealed class when_buffering_writes_until_live : IClassFixture<StreamStoreConnectionFixture>, IDisposable {
	private const int GatedKey = 97;

	private readonly IStreamStoreConnection _conn;
	private readonly IConfiguredConnection _configured;
	private readonly IEventSerializer _serializer = new JsonMessageSerializer();
	private readonly IStreamNameBuilder _namer =
		new PrefixedCamelCaseStreamNameBuilder(nameof(when_buffering_writes_until_live));
	private readonly List<IDisposable> _disposables = [];

	public when_buffering_writes_until_live(StreamStoreConnectionFixture fixture) {
		_conn = fixture.Connection;
		_conn.Connect();
		_configured = new ConfiguredConnection(_conn, _namer, _serializer);
	}

	private string NewStream() => _namer.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());

	private void Append(string streamName, params IMessage[] events) {
		foreach (var @event in events) {
			_conn.AppendToStream(streamName, ExpectedVersion.Any, null, _serializer.Serialize(@event));
		}
	}

	private static RowChanged[] Rows(int from, int count) =>
		Enumerable.Range(from, count).Select(k => new RowChanged(k, $"row {k}")).ToArray();

	private T Track<T>(T disposable) where T : IDisposable {
		_disposables.Add(disposable);
		return disposable;
	}

	[Fact]
	public async Task nothing_is_written_during_catch_up_and_everything_once_at_the_transition() {
		var stream = NewStream();
		Append(stream, Rows(0, 20));
		var rm = Track(new BufferedTestReadModel(_configured));

		rm.StartAsync(stream);
		await rm.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		// Asserted directly: the flush is sequenced ahead of IsLive completing.
		var flush = Assert.Single(rm.Flushes);
		Assert.Equal(20, flush.Rows.Count);
		Assert.Equal(20, rm.HandledBeforeFirstFlush);
		Assert.Equal(19, Assert.Single(flush.Checkpoints).Version);
		Assert.Equal(stream, flush.Checkpoints[0].StreamName);
	}

	[Fact]
	public async Task once_live_each_event_flushes_once_however_many_rows_it_touches() {
		var stream = NewStream();
		var rm = Track(new BufferedTestReadModel(_configured));
		rm.StartAsync(stream);
		await rm.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);
		Assert.Empty(rm.Flushes); // nothing pending at the transition, so nothing was written

		Append(stream, new ManyRowsChanged([1, 2, 3]));

		AssertEx.IsOrBecomesTrue(() => rm.Flushes.Count == 1, TestTimeouts.ThrottleWaitFor);
		var flush = rm.Flushes[0];
		Assert.Equal([1, 2, 3], flush.Rows.Keys.Order());
		Assert.Equal(3, flush.Totals["count"]);
		Assert.Equal(0, Assert.Single(flush.Checkpoints).Version);
	}

	[Fact]
	public async Task an_event_that_changes_nothing_does_not_flush() {
		var stream = NewStream();
		var rm = Track(new BufferedTestReadModel(_configured));
		rm.StartAsync(stream);
		await rm.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		Append(stream, new NothingChanged(), new NothingChanged(), new RowChanged(1, "row 1"));

		// The flush follows the handler, so waiting on the handled count could observe the gap between.
		AssertEx.IsOrBecomesTrue(() => rm.Flushes.Count == 1, TestTimeouts.ThrottleWaitFor);
		var flush = rm.Flushes[0];
		Assert.Equal(3, rm.Handled);
		Assert.Equal([1], flush.Rows.Keys);
		Assert.Equal(2, Assert.Single(flush.Checkpoints).Version); // the checkpoint still covers the two no-ops
	}

	/// <summary>
	/// The reason the checkpoint travels with the event: under load the listener has delivered well
	/// past what the handlers have run, and a flush that persisted that reading would claim rows it
	/// has not written.
	/// </summary>
	[Fact]
	public async Task each_flush_carries_the_checkpoint_of_the_event_it_covers_not_of_what_is_delivered() {
		var stream = NewStream();
		using var handlerGate = new ManualResetEventSlim(false);
		var rm = Track(new BufferedTestReadModel(_configured, handlerGate));
		rm.StartAsync(stream);
		await rm.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		Append(stream, new RowChanged(GatedKey, "parked"));
		Append(stream, Rows(1, 4));
		Assert.True(rm.Parked.Wait(TestTimeouts.ThrottleWaitFor));
		AssertEx.IsOrBecomesTrue(() => rm.GetCheckpoint().Single().Version == 4, TestTimeouts.ThrottleWaitFor);
		Assert.Empty(rm.Flushes);

		handlerGate.Set();
		AssertEx.IsOrBecomesTrue(() => rm.Flushes.Count == 5, TestTimeouts.ThrottleWaitFor);

		Assert.Equal([0, 1, 2, 3, 4], rm.Flushes.Select(f => f.Checkpoints.Single().Version));
		Assert.All(rm.Flushes, f => Assert.Single(f.Rows));
	}

	[Fact]
	public async Task a_failing_transition_flush_retries_without_another_event() {
		var stream = NewStream();
		Append(stream, Rows(0, 3));
		var rm = Track(new BufferedTestReadModel(_configured) { FailNextFlush = true });

		rm.StartAsync(stream);
		await rm.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		var flush = Assert.Single(rm.Flushes);
		Assert.Equal([0, 1, 2], flush.Rows.Keys.Order());
		Assert.Equal(2, flush.Checkpoints.Single().Version);
	}

	[Fact]
	public async Task once_live_waits_until_the_transition_flush_lands() {
		var stream = NewStream();
		Append(stream, Rows(0, 3));
		var rm = Track(new BufferedTestReadModel(_configured) { FailNextFlush = true });
		var ran = false;
		var once = rm.OnceLive(() => ran = true);

		rm.StartAsync(stream);
		await once.WaitAsync(TestTimeouts.ThrottleWaitFor);
		await rm.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		Assert.True(ran);
		Assert.Single(rm.Flushes);
	}

	[Fact]
	public async Task dispose_cancels_a_transition_that_is_still_retrying() {
		var stream = NewStream();
		Append(stream, Rows(0, 3));
		var rm = new BufferedTestReadModel(_configured) { FailEveryFlush = true };
		rm.StartAsync(stream);

		AssertEx.IsOrBecomesTrue(() => rm.FlushAttempts > 0, TestTimeouts.ThrottleWaitFor);
		Assert.False(rm.IsLive.IsCompletedSuccessfully);

		rm.Dispose();
		await Assert.ThrowsAnyAsync<OperationCanceledException>(
			() => rm.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor));
	}

	[Fact]
	public async Task a_transition_flush_that_never_lands_faults_is_live() {
		var stream = NewStream();
		Append(stream, Rows(0, 3));
		var rm = Track(new BufferedTestReadModel(_configured) { FailEveryFlush = true });
		rm.StartAsync(stream);
		var once = rm.OnceLive(() => { });
		var thrown = await Assert.ThrowsAsync<IOException>(
			() => rm.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor));
		Assert.Equal("store down", thrown.Message);
		await Assert.ThrowsAsync<IOException>(() => once.WaitAsync(TestTimeouts.ThrottleWaitFor));
		Assert.Equal(4, rm.FlushAttempts);
		Assert.Empty(rm.Flushes);
	}

	[Fact]
	public async Task within_a_batch_the_last_write_to_a_key_wins() {
		var stream = NewStream();
		Append(stream,
			new RowChanged(1, "first"), new RowRemoved(1),
			new RowChanged(2, "first"), new RowRemoved(2), new RowChanged(2, "second"),
			new RowChanged(3, "first"), new RowChanged(3, "second"));
		var rm = Track(new BufferedTestReadModel(_configured));

		rm.StartAsync(stream);
		await rm.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		var flush = Assert.Single(rm.Flushes);
		Assert.Equal([1], flush.Deletes);
		Assert.Equal(new Dictionary<int, string> { [2] = "second", [3] = "second" }, flush.Rows);
	}

	[Fact]
	public async Task a_category_fed_flush_includes_the_category_stream() {
		// Own namer: this class's other tests write TestAggregate streams into a shared category.
		var namer = new PrefixedCamelCaseStreamNameBuilder(Guid.NewGuid().ToString("N"));
		var configured = new ConfiguredConnection(_conn, namer, _serializer);
		var id = Guid.NewGuid();
		var aggregate = namer.GenerateForAggregate(typeof(TestAggregate), id);
		var category = namer.GenerateForCategory(typeof(TestAggregate));
		foreach (var row in Rows(0, 5)) {
			_conn.AppendToStream(aggregate, ExpectedVersion.Any, null, _serializer.Serialize(row));
		}
		Assert.True(_conn.TryConfirmStream(category, 5));
		var source = Track(new CategoryStream<TestAggregate>(configured));
		var rm = Track(new BufferedTestReadModel(configured));
		source.RelayTo(rm);
		source.Start();
		await rm.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		var flush = Assert.Single(rm.Flushes);
		Assert.Equal(5, flush.Rows.Count);
		var applied = Assert.Single(flush.Checkpoints);
		Assert.Equal(category, applied.StreamName);
		Assert.Equal(4, applied.Version);
	}

	[Fact]
	public async Task flush_retains_the_rows_it_saw_after_the_buffers_clear() {
		var stream = NewStream();
		Append(stream, Rows(0, 3));
		var rm = Track(new BufferedTestReadModel(_configured));
		rm.StartAsync(stream);
		await rm.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		Assert.NotNull(rm.RetainedUpserts);
		Assert.Equal(3, rm.RetainedUpserts.Count);
		Assert.False(rm.BufferStillPending);
	}

	[Fact]
	public async Task a_stream_started_later_is_batched_and_flushed_at_its_own_transition() {
		var stream1 = NewStream();
		var stream2 = NewStream();
		Append(stream1, Rows(0, 2));
		Append(stream2, Rows(10, 10));
		var rm = Track(new BufferedTestReadModel(_configured));
		rm.StartAsync(stream1);
		await rm.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);
		Assert.Single(rm.Flushes);

		rm.StartAsync(stream2);
		await rm.IsLive.WaitAsync(TestTimeouts.ThrottleWaitFor);

		Assert.Equal(2, rm.Flushes.Count);
		var flush = rm.Flushes[1];
		Assert.Equal(10, flush.Rows.Count);
		Assert.Equal(2, flush.Checkpoints.Count);
		Assert.Equal(9, flush.Checkpoints.Single(c => c.StreamName == stream2).Version);
		Assert.Equal(1, flush.Checkpoints.Single(c => c.StreamName == stream1).Version);
	}

	public void Dispose() {
		_disposables.ForEach(d => d.Dispose());
	}

	private sealed record Flushed(
		Dictionary<int, string> Rows,
		HashSet<int> Deletes,
		Dictionary<string, long> Totals,
		IReadOnlyList<StreamCheckpoint> Checkpoints);

	private sealed class BufferedTestReadModel : BufferedReadModelBase,
		IHandle<RowChanged>, IHandle<RowRemoved>, IHandle<ManyRowsChanged>, IHandle<NothingChanged> {
		private readonly ManualResetEventSlim? _handlerGate;
		private readonly WriteBuffer<int, string> _rows;
		private readonly WriteBuffer<string, long> _totals;
		private long _count;

		public BufferedTestReadModel(IConfiguredConnection connection, ManualResetEventSlim? handlerGate = null)
			: base(nameof(BufferedTestReadModel), connection) {
			_handlerGate = handlerGate;
			_rows = CreateBuffer<int, string>();
			_totals = CreateBuffer<string, long>();
			EventStream.Subscribe<RowChanged>(this);
			EventStream.Subscribe<RowRemoved>(this);
			EventStream.Subscribe<ManyRowsChanged>(this);
			EventStream.Subscribe<NothingChanged>(this);
		}

		public List<Flushed> Flushes { get; } = [];
		public int Handled { get; private set; }
		public int HandledBeforeFirstFlush { get; private set; } = -1;
		public bool FailNextFlush { get; set; }
		public bool FailEveryFlush { get; set; }
		public int FlushAttempts { get; private set; }
		public IReadOnlyDictionary<int, string>? RetainedUpserts { get; private set; }
		public bool BufferStillPending => HasPendingWrites;
		public readonly ManualResetEventSlim Parked = new(false);

		public void Handle(RowChanged @event) {
			if (@event.Key == GatedKey) {
				Parked.Set();
				_handlerGate?.Wait(TimeSpan.FromMinutes(2));
			}
			Handled++;
			_rows.Upsert(@event.Key, @event.Value);
		}

		public void Handle(RowRemoved @event) {
			Handled++;
			_rows.Delete(@event.Key);
		}

		public void Handle(ManyRowsChanged @event) {
			Handled++;
			foreach (var key in @event.Keys) {
				_rows.Upsert(key, $"row {key}");
				_totals.Upsert("count", ++_count);
			}
		}

		public void Handle(NothingChanged @event) => Handled++;

		protected override void Flush(IReadOnlyList<StreamCheckpoint> checkpoints) {
			FlushAttempts++;
			if (FailEveryFlush || FailNextFlush) {
				FailNextFlush = false;
				throw new IOException("store down");
			}
			if (HandledBeforeFirstFlush < 0)
				HandledBeforeFirstFlush = Handled;
			RetainedUpserts = _rows.Upserts;
			Flushes.Add(new Flushed(
				new Dictionary<int, string>(_rows.Upserts),
				[.. _rows.Deletes],
				new Dictionary<string, long>(_totals.Upserts),
				checkpoints));
		}
	}

	public record RowChanged(int Key, string Value) : Event;
	public record RowRemoved(int Key) : Event;
	public record ManyRowsChanged(int[] Keys) : Event;
	public record NothingChanged : Event;
}
