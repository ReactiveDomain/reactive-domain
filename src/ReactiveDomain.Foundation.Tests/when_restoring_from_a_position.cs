using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using ReactiveDomain.Testing.EventStore;
using Xunit;

namespace ReactiveDomain.Foundation.Tests;

// A checkpoint carries a version and a position, and restoring uses the version. This asks whether
// the position names the same place — replaying $all from it here rather than in the library, so the
// equivalence is established before anything is built on it. What the replay has to get right is the
// part that would sink a real implementation, and it is not the position: it is which entry belongs
// to the subscription being resumed.
// ReSharper disable once InconsistentNaming
public sealed class when_restoring_from_a_position : IDisposable {
	private readonly MockStreamStoreConnection _conn;
	private readonly IEventSerializer _serializer = new JsonMessageSerializer();
	private readonly IStreamNameBuilder _namer =
		new PrefixedCamelCaseStreamNameBuilder(nameof(when_restoring_from_a_position));
	private readonly IConfiguredConnection _configured;
	private readonly string _stream;

	public when_restoring_from_a_position() {
		_conn = new MockStreamStoreConnection(nameof(when_restoring_from_a_position));
		_conn.Connect();
		_configured = new ConfiguredConnection(_conn, _namer, _serializer);
		_stream = _namer.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());
	}

	public void Dispose() => _conn.Dispose();

	private void Append(int count) {
		for (var i = 0; i < count; i++) {
			_conn.AppendToStream(_stream, ExpectedVersion.Any, null, _serializer.Serialize(new CountedEvent()));
		}
	}

	public record CountedEvent : Event;

	private sealed class CountingModel : SnapshotReadModel, IHandle<CountedEvent> {
		public CountingModel(IConfiguredConnection c, ReadModelState? restore = null)
			: base(nameof(CountingModel), c) {
			EventStream.Subscribe<CountedEvent>(this);
			if (restore is not null) { Restore(restore); }
		}
		public long Applied { get; private set; }
		void IHandle<CountedEvent>.Handle(CountedEvent e) => Applied++;
		protected override void ApplyState(ReadModelState snapshot) => Applied = (long)snapshot.State;
		public override ReadModelState GetState() =>
			new(nameof(CountingModel), GetCheckpoint(), Applied, GetExternalCheckpoints());
	}

	/// <summary>
	/// The stream an entry was delivered on, which is what a subscription resuming from $all has to
	/// match against. For a link this is the projection it lives in; <see cref="RecordedEvent.EventStreamId"/>
	/// is the stream the link points at, and is the same for the original and for every copy of it.
	/// </summary>
	private static string DeliveredOn(RecordedEvent recorded) =>
		recorded is ProjectedEvent projected ? projected.ProjectedStream : recorded.EventStreamId;

	// Replays $all from where a checkpoint left off, taking only what that checkpoint's own
	// subscription would have been handed.
	private long ReplayFrom(StreamCheckpoint checkpoint, Func<RecordedEvent, string> belongsTo) =>
		ReplayFrom(checkpoint.Position!.Value, checkpoint.StreamName, belongsTo);

	private long ReplayFrom(Position from, string stream, Func<RecordedEvent, string> belongsTo) {
		var applied = 0L;
		using var subscription = _conn.SubscribeToAllFrom(
			from,
			recorded => {
				if (belongsTo(recorded) == stream && _serializer.Deserialize(recorded) is CountedEvent) {
					applied++;
				}
			});
		return applied;
	}

	private StreamCheckpoint Checkpoint(ReadModelState snapshot, string stream) =>
		Assert.Single(snapshot.Checkpoints!, c => c.StreamName == stream);

	// A model on all three kinds at once: the aggregate's own stream, the category it belongs to, and
	// the stream of its event type. Every append reaches this model three times, once per
	// subscription, which is what independent streams means — they are not three views of one feed to
	// be deduplicated, they are three feeds.
	private CountingModel StartOnEveryKind(out string category, out string eventType) {
		category = _namer.GenerateForCategory(typeof(TestAggregate));
		eventType = _namer.GenerateForEventType(nameof(CountedEvent));
		var rm = new CountingModel(_configured);
		rm.Start(_stream, null, true);
		rm.Start(category, null, true);
		rm.Start(eventType, null, true);
		return rm;
	}

	[Fact]
	public async Task a_version_and_a_position_name_the_same_place_in_a_stream() {
		Append(6);
		using var rm = new CountingModel(_configured);
		rm.Start(_stream, null, true);
		await rm.IsLive;
		var snapshot = await rm.CaptureConsistentState();
		var checkpoint = Assert.Single(snapshot.Checkpoints!);
		Assert.NotNull(checkpoint.Position);

		Append(4);

		// By version: restore the state and resume the stream after it.
		using var byVersion = new CountingModel(_configured, snapshot);
		AssertEx.IsOrBecomesTrue(() => byVersion.Applied == 10, TestTimeouts.ThrottleWaitFor);

		// By position: restore the same state and replay $all from where it left off.
		var byPosition = (long)snapshot.State + ReplayFrom(checkpoint, DeliveredOn);

		Assert.Equal(byVersion.Applied, byPosition);
	}

	[Fact]
	public async Task a_category_checkpoint_names_the_same_place_as_its_version_does() {
		Append(6);
		using var rm = new CountingModel(_configured);
		rm.Start<TestAggregate>(null, true);
		await rm.IsLive;
		var snapshot = await rm.CaptureConsistentState();
		var checkpoint = Assert.Single(snapshot.Checkpoints!);
		Assert.Equal(_namer.GenerateForCategory(typeof(TestAggregate)), checkpoint.StreamName);

		Append(4);

		using var byVersion = new CountingModel(_configured, snapshot);
		AssertEx.IsOrBecomesTrue(() => byVersion.Applied == 10, TestTimeouts.ThrottleWaitFor);

		var byPosition = (long)snapshot.State + ReplayFrom(checkpoint, DeliveredOn);

		Assert.Equal(byVersion.Applied, byPosition);
	}

	[Fact]
	public async Task a_position_resumes_after_the_entry_it_names() {
		Append(3);
		using var rm = new CountingModel(_configured);
		rm.Start(_stream, null, true);
		await rm.IsLive;
		var checkpoint = Assert.Single((await rm.CaptureConsistentState()).Checkpoints!);

		// Everything was applied, so resuming from here must find nothing — a position that included
		// the entry it names would re-apply the last event on every restore.
		Assert.Equal(0, ReplayFrom(checkpoint, DeliveredOn));
	}

	[Fact]
	public async Task matching_an_entry_by_the_stream_it_points_at_counts_it_once_per_copy() {
		// The trap that would sink an $all-based restore. One appended event is three entries in $all
		// — the original, the category link, the event-type link — and all three report the stream the
		// original lives on. Matching on that applies each event once per projection that copied it,
		// while the category-fed case below matches nothing at all: opposite errors, one filter.
		Append(3);
		using var rm = new CountingModel(_configured);
		rm.Start(_stream, null, true);
		await rm.IsLive;
		var snapshot = await rm.CaptureConsistentState();
		var checkpoint = Assert.Single(snapshot.Checkpoints!);

		Append(2);

		Assert.Equal(2, ReplayFrom(checkpoint, DeliveredOn));

		// Eight, not the six the two new events copied three ways would suggest. A link is written
		// after the event it points at, so a position naming an original sits *before* that same
		// event's own copies: the wrong filter re-applies an event the snapshot had already applied,
		// as well as counting the new ones three times over.
		Assert.Equal(8, ReplayFrom(checkpoint, recorded => recorded.EventStreamId));
	}

	[Fact]
	public async Task matching_a_category_entry_by_the_stream_it_points_at_finds_nothing() {
		Append(3);
		using var rm = new CountingModel(_configured);
		rm.Start<TestAggregate>(null, true);
		await rm.IsLive;
		var checkpoint = Assert.Single((await rm.CaptureConsistentState()).Checkpoints!);

		Append(2);

		Assert.Equal(2, ReplayFrom(checkpoint, DeliveredOn));
		// A link's EventStreamId is the aggregate stream, never the category it was copied into.
		Assert.Equal(0, ReplayFrom(checkpoint, recorded => recorded.EventStreamId));
	}

	[Fact]
	public async Task every_subscribed_stream_is_checkpointed_independently() {
		Append(3);
		using var rm = StartOnEveryKind(out var category, out var eventType);
		await rm.IsLive;
		AssertEx.IsOrBecomesTrue(() => rm.Applied == 9, TestTimeouts.ThrottleWaitFor);

		var snapshot = await rm.CaptureConsistentState();

		Assert.Equal(3, snapshot.Checkpoints!.Count);
		foreach (var stream in new[] { _stream, category, eventType }) {
			var checkpoint = Checkpoint(snapshot, stream);
			Assert.Equal(2, checkpoint.Version);
			Assert.NotNull(checkpoint.Position);
		}

		// Same version on each, and three different places in the log: a projection's copy is its own
		// entry, written after the one it points at. One position could not stand for all three.
		var positions = snapshot.Checkpoints!.Select(c => c.Position!.Value).ToList();
		Assert.Equal(3, positions.Distinct().Count());
	}

	[Fact]
	public async Task each_stream_replays_from_its_own_position_to_the_same_state() {
		Append(3);
		using var rm = StartOnEveryKind(out var category, out var eventType);
		await rm.IsLive;
		AssertEx.IsOrBecomesTrue(() => rm.Applied == 9, TestTimeouts.ThrottleWaitFor);
		var snapshot = await rm.CaptureConsistentState();

		Append(2);

		using var byVersion = new CountingModel(_configured, snapshot);
		AssertEx.IsOrBecomesTrue(() => byVersion.Applied == 15, TestTimeouts.ThrottleWaitFor);

		// What a consumer restoring from $all has to do: take each recorded stream on its own terms,
		// resume it from its own position, and keep them apart. Summed, that is the model's state.
		var byPosition = (long)snapshot.State + snapshot.Checkpoints!.Sum(c => ReplayFrom(c, DeliveredOn));

		Assert.Equal(byVersion.Applied, byPosition);
	}

	[Fact]
	public async Task one_streams_position_does_not_resume_another() {
		Append(3);
		using var rm = StartOnEveryKind(out var category, out _);
		await rm.IsLive;
		AssertEx.IsOrBecomesTrue(() => rm.Applied == 9, TestTimeouts.ThrottleWaitFor);
		var snapshot = await rm.CaptureConsistentState();

		Append(2);

		// The category resumed from its own position sees the two new copies.
		Assert.Equal(2, ReplayFrom(Checkpoint(snapshot, category), DeliveredOn));

		// Resumed from the aggregate stream's position instead, it sees three: the link for the event
		// the aggregate stream had already reached sits after that event's own entry, so a position
		// borrowed from one stream re-applies an event on another. Independent streams, independent
		// positions.
		Assert.Equal(3,
			ReplayFrom(Checkpoint(snapshot, _stream).Position!.Value, category, DeliveredOn));
	}
}
