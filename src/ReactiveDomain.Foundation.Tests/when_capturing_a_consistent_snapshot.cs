using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using ReactiveDomain.Testing.EventStore;
using Xunit;

namespace ReactiveDomain.Foundation.Tests;

// The invariant under test: the state a snapshot carries is the fold of exactly the events its
// checkpoint names. Every case here is a way of asking whether the two can be made to disagree —
// with the handler wedged, under live traffic, and across two streams at once.
// ReSharper disable once InconsistentNaming
public sealed class when_capturing_a_consistent_snapshot : IDisposable {
	private readonly MockStreamStoreConnection _conn;
	private readonly IEventSerializer _serializer = new JsonMessageSerializer();
	private readonly IStreamNameBuilder _namer =
		new PrefixedCamelCaseStreamNameBuilder(nameof(when_capturing_a_consistent_snapshot));
	private readonly IConfiguredConnection _configured;

	public when_capturing_a_consistent_snapshot() {
		_conn = new MockStreamStoreConnection(nameof(when_capturing_a_consistent_snapshot));
		_conn.Connect();
		_configured = new ConfiguredConnection(_conn, _namer, _serializer);
	}

	public void Dispose() => _conn.Dispose();

	private string NewStream() => _namer.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());

	private void Append(string stream, int count) {
		for (var i = 0; i < count; i++) {
			_conn.AppendToStream(stream, ExpectedVersion.Any, null, _serializer.Serialize(new CountedEvent()));
		}
	}

	public record CountedEvent : Event;

	private sealed class CountingModel : SnapshotReadModel, IHandle<CountedEvent> {
		private readonly ManualResetEventSlim _gate = new(true);
		public CountingModel(IConfiguredConnection c, ReadModelState? restore = null)
			: base(nameof(CountingModel), c) {
			EventStream.Subscribe<CountedEvent>(this);
			if (restore is not null) { Restore(restore); }
		}

		public long Applied { get; private set; }
		public void Wedge() => _gate.Reset();
		public void Unwedge() => _gate.Set();

		// Enough work per event that a writer outruns the queue, so a capture under live traffic
		// actually meets a non-empty queue. Without it the queue is drained by the time the capture
		// is asked for and the case being tested never arises.
		public bool Deliberate { get; set; }

		void IHandle<CountedEvent>.Handle(CountedEvent e) {
			_gate.Wait();
			if (Deliberate) { Thread.SpinWait(2_000); }
			Applied++;
		}

		protected override void ApplyState(ReadModelState snapshot) => Applied = (long)snapshot.State;
		public override ReadModelState GetState() =>
			new(nameof(CountingModel), GetCheckpoint(), Applied, GetExternalCheckpoints());

		protected override void Dispose(bool disposing) {
			_gate.Set();
			base.Dispose(disposing);
		}
	}

	// A checkpoint resumes *after* its version, so a state holding n events must be checkpointed at
	// version n-1. Any other pairing is a snapshot that loses or repeats events on restore.
	private static void AssertDescribesState(ReadModelState snapshot, long applied) {
		Assert.Equal(applied, (long)snapshot.State);
		AssertSelfConsistent(snapshot);
	}

	// A version is the index of a stream's last applied event, so one more than it is that prefix's
	// length. Summed over the streams, that has to be exactly what the state counted.
	// A capture taken against an empty queue cannot distinguish a cut from a checkpoint re-read
	// afterwards, so these tests wait until there is something in flight to be wrong about.
	private static bool AwaitQueueDepth(CountingModel rm, Task writer) =>
		SpinWait.SpinUntil(() => rm.MessageCount > 0 || writer.IsCompleted, TestTimeouts.ThrottleWaitFor)
		&& rm.MessageCount > 0;

	private static void AssertSelfConsistent(ReadModelState snapshot) =>
		Assert.Equal(snapshot.Checkpoints!.Sum(c => (c.Version ?? -1) + 1), (long)snapshot.State);

	[Fact]
	public async Task the_checkpoint_describes_the_state_it_was_captured_with() {
		var stream = NewStream();
		Append(stream, 10);
		using var rm = new CountingModel(_configured);
		rm.Start(stream, null, true);
		await rm.IsLive;

		AssertDescribesState(await rm.CaptureConsistentState(), rm.Applied);
	}

	[Fact]
	public async Task a_capture_waits_for_what_the_listener_already_handed_over() {
		var stream = NewStream();
		using var rm = new CountingModel(_configured);
		rm.Start(stream, null, false);
		await rm.IsLive;

		// Wedged, so the listener delivers into the queue while nothing can be applied. This is the
		// state in which reading the checkpoint from the side reports 3 events against a state that
		// holds none.
		rm.Wedge();
		Append(stream, 3);
		AssertEx.IsOrBecomesTrue(
			() => rm.GetCheckpoint().FirstOrDefault()?.Version == 2, TestTimeouts.ThrottleWaitFor);
		Assert.Equal(0, rm.Applied);

		var capturing = rm.CaptureConsistentState();
		Assert.False(capturing.IsCompleted, "captured a cut the model had not reached");

		rm.Unwedge();
		AssertDescribesState(await capturing, 3);
	}

	[Fact]
	public async Task a_capture_taken_under_live_traffic_describes_its_own_cut() {
		var stream = NewStream();
		Append(stream, 5);
		using var rm = new CountingModel(_configured) { Deliberate = true };
		rm.Start(stream, null, true);
		await rm.IsLive;

		// The guaranteed cut: wedged, the burst piles into the queue, and the capture is requested
		// against that provably non-empty queue — no race decides whether the case arises. Unwedging
		// lets it drain with the writer still appending behind the marker, so the cut lands mid-delivery.
		rm.Wedge();
		var writer = Task.Run(() => Append(stream, 150));
		AssertEx.IsOrBecomesTrue(() => rm.MessageCount > 0, TestTimeouts.ThrottleWaitFor);
		var capturing = rm.CaptureConsistentState();
		rm.Unwedge();
		AssertSelfConsistent(await capturing);
		// Opportunistic extra cuts while the burst drains — every one that meets a non-empty queue
		// must also be self-consistent.
		while (AwaitQueueDepth(rm, writer)) {
			AssertSelfConsistent(await rm.CaptureConsistentState());
		}
		await writer;
		AssertSelfConsistent(await rm.CaptureConsistentState());
	}

	[Fact]
	public async Task restoring_a_capture_taken_across_a_stalled_cut_neither_loses_nor_repeats_an_event() {
		var stream = NewStream();
		Append(stream, 5);
		using var rm = new CountingModel(_configured);
		rm.Start(stream, null, true);
		await rm.IsLive;

		// Wedged, so delivery and application are driven apart on purpose rather than by racing a
		// writer. Everything appended from here is handed to the queue and none of it is applied.
		rm.Wedge();
		Append(stream, 10);
		AssertEx.IsOrBecomesTrue(
			() => rm.GetCheckpoint()[0].Version == 14, TestTimeouts.ThrottleWaitFor);
		Assert.Equal(5, rm.Applied);

		// The barrier goes in behind those ten and ahead of the next five, so the cut is known before
		// anything drains: state of 15, checkpointed at 14.
		var capturing = rm.CaptureConsistentState();
		Append(stream, 5);
		AssertEx.IsOrBecomesTrue(
			() => rm.GetCheckpoint()[0].Version == 19, TestTimeouts.ThrottleWaitFor);

		rm.Unwedge();
		var snapshot = await capturing;
		AssertDescribesState(snapshot, 15);

		// Restore resumes after 14 and applies the last five. Short means the capture named events it
		// had not applied; over means it applied events it did not name.
		using var restored = new CountingModel(_configured, snapshot);
		AssertEx.IsOrBecomesTrue(() => restored.Applied == 20, TestTimeouts.ThrottleWaitFor,
			$"restored {restored.Applied} of 20 appended");
	}

	[Fact]
	public async Task a_capture_spans_every_listener_at_one_cut() {
		var first = NewStream();
		var second = NewStream();
		Append(first, 4);
		Append(second, 6);

		using var rm = new CountingModel(_configured) { Deliberate = true };
		rm.Start(first, null, true);
		rm.Start(second, null, true);
		await rm.IsLive;

		// Both streams at one cut: the state has to be the sum of the two prefixes, so a listener that
		// kept delivering past the sample shows up as a state ahead of its checkpoints. The wedge makes
		// the first cut guaranteed rather than raced — see the single-stream case above.
		rm.Wedge();
		var writer = Task.Run(() => { for (var i = 0; i < 75; i++) { Append(first, 1); Append(second, 1); } });
		AssertEx.IsOrBecomesTrue(() => rm.MessageCount > 0, TestTimeouts.ThrottleWaitFor);
		var capturing = rm.CaptureConsistentState();
		rm.Unwedge();
		AssertSelfConsistent(await capturing);
		while (AwaitQueueDepth(rm, writer)) {
			AssertSelfConsistent(await rm.CaptureConsistentState());
		}
		await writer;
		AssertSelfConsistent(await rm.CaptureConsistentState());
	}

	[Fact]
	public async Task a_capture_leaves_the_listeners_delivering() {
		var stream = NewStream();
		var other = NewStream();
		Append(stream, 2);
		Append(other, 2);
		using var rm = new CountingModel(_configured);
		rm.Start(stream, null, true);
		rm.Start(other, null, true);
		await rm.IsLive;

		// Two listeners means the capture holds them; the hold has to be released whether or not the
		// capture succeeded, or the model is deaf from here on.
		await rm.CaptureConsistentState();

		Append(stream, 1);
		AssertEx.IsOrBecomesTrue(() => rm.Applied == 5, TestTimeouts.ThrottleWaitFor,
			"a listener was left held after the capture completed");
	}

	[Fact]
	public async Task successive_captures_of_a_model_order_by_what_they_cover() {
		var stream = NewStream();
		Append(stream, 3);
		using var rm = new CountingModel(_configured);
		rm.Start(stream, null, true);
		await rm.IsLive;

		var earlier = await rm.CaptureConsistentState();
		Append(stream, 4);
		AssertEx.IsOrBecomesTrue(() => rm.Applied == 7, TestTimeouts.ThrottleWaitFor);
		var later = await rm.CaptureConsistentState();

		Assert.Equal(CheckpointOrder.Before, earlier.Compare(later));
		Assert.Equal(CheckpointOrder.After, later.Compare(earlier));
		Assert.Equal(CheckpointOrder.Equal, earlier.Compare(earlier));
	}

	[Fact]
	public async Task a_model_fed_outside_its_listeners_refuses_to_capture() {
		var stream = NewStream();
		Append(stream, 3);
		using var rm = new CountingModel(_configured);
		rm.Start(stream, null, true);
		await rm.IsLive;
		Assert.False(rm.HasUnreplayableInput);

		rm.DirectApply(new CountedEvent());

		Assert.True(rm.HasUnreplayableInput);
		// Synchronously, not as a faulted task: a caller that never awaits still has to hear it.
		Assert.Throws<InvalidOperationException>(() => { _ = rm.CaptureConsistentState(); });
	}

	[Fact]
	public async Task publishing_into_a_model_is_the_same_refusal() {
		var stream = NewStream();
		Append(stream, 3);
		using var rm = new CountingModel(_configured);
		rm.Start(stream, null, true);
		await rm.IsLive;

		rm.Publish(new CountedEvent());

		Assert.True(rm.HasUnreplayableInput);
		// Synchronously, not as a faulted task: a caller that never awaits still has to hear it.
		Assert.Throws<InvalidOperationException>(() => { _ = rm.CaptureConsistentState(); });
	}

	[Fact]
	public async Task a_model_with_a_stream_still_reading_has_no_cut_to_capture() {
		var stream = NewStream();
		Append(stream, 5);
		using var rm = new CountingModel(_configured);
		try {
			// Wedged, so the read cannot drain and the model does not go live. Its events are already
			// reaching the queue through the model's own Handle, named by no listener checkpoint.
			rm.Wedge();
			rm.StartAsync(stream);

			Assert.Throws<InvalidOperationException>(() => { _ = rm.CaptureConsistentState(); });
			Assert.False(rm.IsLive.IsCompleted);
		} finally {
			rm.Unwedge();
		}

		await rm.IsLive;
		AssertDescribesState(await rm.CaptureConsistentState(), 5);
	}

	[Fact]
	public async Task a_stream_cannot_be_started_inside_the_capture_window() {
		var stream = NewStream();
		Append(stream, 3);
		using var rm = new CountingModel(_configured);
		rm.Start(stream, null, true);
		await rm.IsLive;

		// Wedged after going live, so the capture is outstanding while the model is otherwise idle.
		rm.Wedge();
		Append(stream, 1);
		var capturing = rm.CaptureConsistentState();
		Assert.False(capturing.IsCompleted);

		Assert.Throws<InvalidOperationException>(() => rm.Start(NewStream(), null, false));

		rm.Unwedge();
		AssertDescribesState(await capturing, 4);
		// The refused start left nothing behind: the model still captures once the window closes.
		AssertDescribesState(await rm.CaptureConsistentState(), 4);
	}

	[Fact]
	public async Task a_disposed_model_releases_a_capture_it_can_no_longer_finish() {
		var stream = NewStream();
		Append(stream, 2);
		using var rm = new CountingModel(_configured);
		rm.Start(stream, null, true);
		await rm.IsLive;

		// The queue is stopped, so the barrier that would answer this can never be dequeued. The
		// caller has to be let go rather than left waiting on a cut nothing will reach.
		rm.Dispose();
		var capturing = rm.CaptureConsistentState();

		await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capturing);
	}
}
