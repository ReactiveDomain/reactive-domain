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
	public void Handle(PositionTestEvent @event) => Handled++;

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
	public void a_source_that_has_delivered_nothing_suppresses_both_projections() {
		Start(_early, null, true);
		AssertEx.IsOrBecomesTrue(() => Handled == 3, TestTimeouts.ThrottleWaitFor);
		Assert.NotNull(HighWaterMark);

		// A second stream with nothing on it reports no position. Projecting over the rest would
		// claim a reach and a coverage the model cannot stand behind.
		Start(_namer.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid()), null, false);

		Assert.Null(HighWaterMark);
		Assert.Null(LowestAppliedPosition);
	}

	public record PositionTestEvent : Event;
}
