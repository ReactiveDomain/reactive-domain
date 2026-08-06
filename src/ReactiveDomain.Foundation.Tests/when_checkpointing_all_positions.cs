using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests;

// A checkpoint carries two clocks. The stream version resumes that stream; the $all position is the
// only one comparable across streams, so it is what a multi-stream model projects a watermark from.
// ReSharper disable once InconsistentNaming
public sealed class when_checkpointing_all_positions :
	ReadModelBase,
	IHandle<when_checkpointing_all_positions.PositionTestEvent>,
	IClassFixture<StreamStoreConnectionFixture> {

	private readonly IStreamStoreConnection _conn;
	private static readonly IEventSerializer _serializer = new JsonMessageSerializer();
	private static readonly IStreamNameBuilder _namer =
		new PrefixedCamelCaseStreamNameBuilder(nameof(when_checkpointing_all_positions));

	private readonly string _early;
	private readonly string _late;

	public when_checkpointing_all_positions(StreamStoreConnectionFixture fixture)
		: base(nameof(when_checkpointing_all_positions),
			new ConfiguredConnection(fixture.Connection, _namer, _serializer)) {
		_conn = fixture.Connection;
		_conn.Connect();
		EventStream.Subscribe<PositionTestEvent>(this);

		_early = _namer.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());
		_late = _namer.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());

		// Appended in order, so _late's events sit further along the all-stream than _early's.
		Append(_early, 3);
		Append(_late, 3);
		_conn.TryConfirmStream(_early, 3);
		_conn.TryConfirmStream(_late, 3);
	}

	private void Append(string stream, int count) {
		for (var i = 0; i < count; i++) {
			_conn.AppendToStream(stream, ExpectedVersion.Any, null,
				_serializer.Serialize(new PositionTestEvent()));
		}
	}

	public int Handled { get; private set; }
	void IHandle<PositionTestEvent>.Handle(PositionTestEvent @event) => Handled++;

	[Fact]
	public void a_checkpoint_carries_the_all_position_of_the_last_event_applied() {
		Start(_early, null, true);
		AssertEx.IsOrBecomesTrue(() => Handled == 3, TestTimeouts.ThrottleWaitFor);

		var checkpoint = Assert.Single(GetCheckpoint());
		Assert.Equal(_early, checkpoint.StreamName);
		Assert.Equal(2, checkpoint.Version);
		Assert.NotNull(checkpoint.Position);
	}

	[Fact]
	public void the_high_water_mark_is_the_furthest_stream_and_the_watermark_the_nearest() {
		Start(_early, null, true);
		Start(_late, null, true);
		AssertEx.IsOrBecomesTrue(() => Handled == 6, TestTimeouts.ThrottleWaitFor);

		var byStream = GetCheckpoint().ToDictionary(c => c.StreamName, c => c.Position!.Value);
		Assert.True(byStream[_late] > byStream[_early]);

		// Same two numbers, opposite ends: how far the model reached, versus how far every one of its
		// sources has been applied through.
		Assert.Equal(byStream[_late], HighWaterMark);
		Assert.Equal(byStream[_early], LowestAppliedPosition);
	}

	// A category start is the common case, and it delivers link copies rather than the originals.
	// Those carry positions of their own, so a category-fed model checkpoints like any other.
	[Fact]
	public void a_category_stream_carries_positions_too() {
		Start<TestAggregate>(null, true);
		// At least this test's six: the category also holds what the class's other tests appended,
		// since they share one connection.
		AssertEx.IsOrBecomesTrue(() => Handled >= 6, TestTimeouts.ThrottleWaitFor);

		var checkpoint = Assert.Single(GetCheckpoint());
		Assert.Equal(_namer.GenerateForCategory(typeof(TestAggregate)), checkpoint.StreamName);
		Assert.NotNull(checkpoint.Position);
		Assert.NotNull(HighWaterMark);
	}

	[Fact]
	public void a_model_with_no_listeners_projects_nothing() {
		Assert.Null(HighWaterMark);
		Assert.Null(LowestAppliedPosition);
		Assert.Empty(GetCheckpoint());
	}

	[Fact]
	public void a_source_that_has_delivered_nothing_suppresses_only_the_watermark() {
		Start(_early, null, true);
		AssertEx.IsOrBecomesTrue(() => Handled == 3, TestTimeouts.ThrottleWaitFor);
		var reached = HighWaterMark;
		Assert.NotNull(reached);

		// A second stream with nothing on it reports no position. The two projections part company
		// here: the model still reached everything it reached, so leaving the silent source out can
		// only understate the reach — but claiming coverage through the nearest of the rest would
		// speak for a source that has said nothing.
		Start(_namer.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid()), null, false);

		Assert.Equal(reached, HighWaterMark);
		Assert.Null(LowestAppliedPosition);
	}

	[Fact]
	public void a_stream_that_has_delivered_nothing_checkpoints_no_version() {
		var empty = _namer.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());
		Start(empty, null, false);

		var checkpoint = Assert.Single(GetCheckpoint());
		Assert.Equal(empty, checkpoint.StreamName);
		// Not 0. Version 0 is this stream's first event, and it has not been delivered — resuming
		// from it would step over the event the checkpoint is meant to precede.
		Assert.Null(checkpoint.Version);
		Assert.Null(checkpoint.Position);
	}

	[Fact]
	public void a_resumed_stream_reports_its_resume_point_before_it_delivers_anything() {
		// _early's last event is version 2, so resuming from there delivers nothing. The resume point
		// is still a version this stream reached — unlike the zero that stands in for no resume point
		// — so it is reportable straight away.
		Start(_early, 2, true);

		var checkpoint = Assert.Single(GetCheckpoint());
		Assert.Equal(2, checkpoint.Version);
		Assert.Equal(0, Handled);
	}

	public record PositionTestEvent : Event;
}
