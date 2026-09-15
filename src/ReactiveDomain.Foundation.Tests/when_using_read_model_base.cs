using System.Text;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests;

// ReSharper disable once InconsistentNaming
public sealed class when_using_read_model_base :
	ReadModelBase,
	IHandle<when_using_read_model_base.ReadModelTestEvent>,
	IClassFixture<StreamStoreConnectionFixture> {

	private readonly IStreamStoreConnection _conn;
	private static readonly IEventSerializer _serializer = new JsonMessageSerializer();
	private static readonly IStreamNameBuilder _namer =
		new PrefixedCamelCaseStreamNameBuilder(nameof(when_using_read_model_base));

	private readonly string _stream1;
	private readonly string _stream2;

	public when_using_read_model_base(StreamStoreConnectionFixture fixture)
		: base(nameof(when_using_read_model_base), new ConfiguredConnection(fixture.Connection, _namer, _serializer)) {
		_conn = fixture.Connection;
		_conn.Connect();

		// ReSharper disable once RedundantTypeArgumentsOfMethod
		EventStream.Subscribe<ReadModelTestEvent>(this);

		_stream1 = _namer.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());
		_stream2 = _namer.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());

		AppendEvents(10, _conn, _stream1, 2);
		AppendEvents(10, _conn, _stream2, 3);

		_conn.TryConfirmStream(_stream1, 10);
		_conn.TryConfirmStream(_stream2, 10);
		_conn.TryConfirmStream(_namer.GenerateForCategory(typeof(TestAggregate)), 20);
	}

	private void AppendEvents(
		int numEventsToBeSent,
		IStreamStoreConnection conn,
		string streamName,
		int value) {
		for (int evtNumber = 0; evtNumber < numEventsToBeSent; evtNumber++) {
			var evt = new ReadModelTestEvent(evtNumber, value);
			conn.AppendToStream(streamName, ExpectedVersion.Any, null, _serializer.Serialize(evt));
		}
	}
	[Fact]
	public void can_start_streams_by_aggregate() {
		var aggId = Guid.NewGuid();
		var s1 = _namer.GenerateForAggregate(typeof(TestAggregate), aggId);
		AppendEvents(1, _conn, s1, 7);
		Start<TestAggregate>(aggId);
		AssertEx.AtLeastModelVersion(this, 1, TestTimeouts.ThrottleWaitFor, msg: $"Expected 1 got {Version}");
		AssertEx.IsOrBecomesTrue(() => Sum == 7, TestTimeouts.ThrottleWaitFor);
	}
	[Fact]
	public void can_start_streams_by_aggregate_category() {

		var s1 = _namer.GenerateForAggregate(typeof(ReadModelTestCategoryAggregate), Guid.NewGuid());
		AppendEvents(1, _conn, s1, 7);
		var s2 = _namer.GenerateForAggregate(typeof(ReadModelTestCategoryAggregate), Guid.NewGuid());
		AppendEvents(1, _conn, s2, 5);
		Start<ReadModelTestCategoryAggregate>(null, true);

		AssertEx.AtLeastModelVersion(this, 2, TestTimeouts.ThrottleWaitFor, msg: $"Expected 2 got {Version}");
		AssertEx.IsOrBecomesTrue(() => Sum == 12, TestTimeouts.ThrottleWaitFor);
	}
	[Fact]
	public void can_read_one_stream() {
		Start(_stream1);
		AssertEx.AtLeastModelVersion(this, 10, TestTimeouts.ThrottleWaitFor, msg: $"Expected 10 got {Version}");
		AssertEx.IsOrBecomesTrue(() => Sum == 20, TestTimeouts.ThrottleWaitFor);
		//confirm checkpoints
		Assert.Equal(_stream1, GetCheckpoint()[0].StreamName);
		Assert.Equal(9, GetCheckpoint()[0].Version);
	}
	[Fact]
	public void can_read_two_streams() {
		Start(_stream1);
		Start(_stream2);
		AssertEx.AtLeastModelVersion(this, 20, TestTimeouts.ThrottleWaitFor, msg: $"Expected 20 got {Version}");
		AssertEx.IsOrBecomesTrue(() => Sum == 50, TestTimeouts.ThrottleWaitFor);
		//confirm checkpoints
		Assert.Equal(_stream1, GetCheckpoint()[0].StreamName);
		Assert.Equal(9, GetCheckpoint()[0].Version);
		Assert.Equal(_stream2, GetCheckpoint()[1].StreamName);
		Assert.Equal(9, GetCheckpoint()[1].Version);
	}
	[Fact]
	public void can_wait_for_one_stream_to_go_live() {
		Start(_stream1, null, true);
		AssertEx.AtLeastModelVersion(this, 10, TestTimeouts.ThrottleWaitFor, msg: $"Expected 10 got {Version}");
		AssertEx.IsOrBecomesTrue(() => Sum == 20, TestTimeouts.ThrottleWaitFor);
	}
	[Fact]
	public void can_wait_for_two_streams_to_go_live() {
		Start(_stream1, null, true);
		AssertEx.AtLeastModelVersion(this, 10, TestTimeouts.ThrottleWaitFor, msg: $"Expected 10 got {Version}");
		AssertEx.IsOrBecomesTrue(() => Sum == 20, TestTimeouts.ThrottleWaitFor);

		Start(_stream2, null, true);
		AssertEx.AtLeastModelVersion(this, 20, TestTimeouts.ThrottleWaitFor, msg: $"Expected 20 got {Version}");
		AssertEx.IsOrBecomesTrue(() => Sum == 50, TestTimeouts.ThrottleWaitFor);
	}
	[Fact]
	public void can_listen_to_one_stream() {
		Start(_stream1);
		AssertEx.AtLeastModelVersion(this, 10, TestTimeouts.ThrottleWaitFor, msg: $"Expected 10 got {Version}");
		AssertEx.IsOrBecomesTrue(() => Sum == 20, TestTimeouts.ThrottleWaitFor);
		//add more messages
		AppendEvents(10, _conn, _stream1, 5);
		AssertEx.AtLeastModelVersion(this, 20, TestTimeouts.ThrottleWaitFor, msg: $"Expected 20 got {Version}");
		AssertEx.IsOrBecomesTrue(() => Sum == 70, TestTimeouts.ThrottleWaitFor);
		//confirm checkpoints
		Assert.Equal(_stream1, GetCheckpoint()[0].StreamName);
		Assert.Equal(19, GetCheckpoint()[0].Version);
		Assert.Equal(_stream1, GetCheckpoint()[0].StreamName);
		Assert.Equal(19, GetCheckpoint()[0].Version);
	}
	[Fact]
	public void can_listen_to_two_streams() {
		Start(_stream1);
		Start(_stream2);
		AssertEx.AtLeastModelVersion(this, 20, TestTimeouts.ThrottleWaitFor, msg: $"Expected 20 got {Version}");
		AssertEx.IsOrBecomesTrue(() => Sum == 50, TestTimeouts.ThrottleWaitFor);
		//add more messages
		AppendEvents(10, _conn, _stream1, 5);
		AppendEvents(10, _conn, _stream2, 7);
		AssertEx.AtLeastModelVersion(this, 40, TestTimeouts.ThrottleWaitFor, msg: $"Expected 40 got {Version}");
		AssertEx.IsOrBecomesTrue(() => Sum == 170, TestTimeouts.ThrottleWaitFor);
		//confirm checkpoints
		Assert.Equal(_stream1, GetCheckpoint()[0].StreamName);
		Assert.Equal(19, GetCheckpoint()[0].Version);
		Assert.Equal(_stream2, GetCheckpoint()[1].StreamName);
		Assert.Equal(19, GetCheckpoint()[1].Version);
	}
	[Fact]
	public void can_use_checkpoint_on_one_stream() {
		//restore state
		var checkPoint = 8L;//Zero based, ignore the first 9 events
		Sum = 18;
		//start at the checkpoint
		Start(_stream1, checkPoint);
		//add the one recorded event
		AssertEx.AtLeastModelVersion(this, 1, TestTimeouts.ThrottleWaitFor, msg: $"Expected 1 got {Version}");
		AssertEx.IsOrBecomesTrue(() => Sum == 20, TestTimeouts.ThrottleWaitFor);
		//add more messages
		AppendEvents(10, _conn, _stream1, 5);
		AssertEx.AtLeastModelVersion(this, 11, TestTimeouts.ThrottleWaitFor, msg: $"Expected 11 got {Version}");
		AssertEx.IsOrBecomesTrue(() => Sum == 70, TestTimeouts.ThrottleWaitFor);
		//confirm checkpoints
		Assert.Equal(_stream1, GetCheckpoint()[0].StreamName);
		Assert.Equal(19, GetCheckpoint()[0].Version);
	}
	[Fact]
	public void can_use_checkpoint_on_two_streams() {
		//restore state
		var checkPoint1 = 8L;//Zero based, ignore the first 9 events
		var checkPoint2 = 5L;//Zero based, ignore the first 6 events
		Sum = (9 * 2) + (6 * 3);
		Start(_stream1, checkPoint1);
		Start(_stream2, checkPoint2);
		//add the recorded events 2 on stream 1 & 5 on stream 2
		AssertEx.AtLeastModelVersion(this, 5, TestTimeouts.ThrottleWaitFor, msg: $"Expected 5 got {Version}");
		AssertEx.IsOrBecomesTrue(() => Sum == 50, TestTimeouts.ThrottleWaitFor, msg: $"Expected 50 got {Sum}");
		//add more messages
		AppendEvents(10, _conn, _stream1, 5);
		AppendEvents(10, _conn, _stream2, 7);
		AssertEx.AtLeastModelVersion(this, 25, TestTimeouts.ThrottleWaitFor, msg: $"Expected 25 got {Version}");
		AssertEx.IsOrBecomesTrue(() => Sum == 170, TestTimeouts.ThrottleWaitFor);
		//confirm checkpoints
		Assert.Equal(_stream1, GetCheckpoint()[0].StreamName);
		Assert.Equal(19, GetCheckpoint()[0].Version);
		Assert.Equal(_stream2, GetCheckpoint()[1].StreamName);
		Assert.Equal(19, GetCheckpoint()[1].Version);
	}
	[Fact]
	public void cannot_listen_to_the_same_stream_twice() {
		Assert.Equal(0, Version);
		Start(_stream1);
		AssertEx.AtLeastModelVersion(this, 10, TestTimeouts.ThrottleWaitFor, msg: $"Expected 10 got {Version}");

		Assert.Throws<InvalidOperationException>(() => Start(_stream1));

		AppendEvents(10, _conn, _stream1, 5);
		AssertEx.IsOrBecomesTrue(() => Sum == 70, TestTimeouts.ThrottleWaitFor, msg: $"Expected 70 got {Sum}");
		Assert.Single(GetCheckpoint());
	}

	[Fact]
	public void going_live_does_not_count_as_a_version() {
		// The transition arrives behind the history, so a count read as soon as the history is folded
		// is read before the message under test: it reports 10 whether or not the transition counts.
		// Waiting for the handler proves the message was dispatched; waiting for the queue to drain
		// then leaves nothing that could still increment.
		using var live = new ManualResetEventSlim(false);
		EventStream.Subscribe(new AdHocHandler<StreamStoreMsgs.CatchupSubscriptionBecameLive>(_ => live.Set()));

		Start(_stream1);

		Assert.True(live.Wait(TestTimeouts.ThrottleWaitFor), "The subscription never reported going live.");
		AssertEx.IsOrBecomesTrue(() => Idle, TestTimeouts.ThrottleWaitFor);
		Assert.Equal(10, Version);
	}

	[Fact]
	public void an_unknown_type_in_history_is_skipped() {
		var stream = _namer.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());
		AppendEvents(2, _conn, stream, 1);
		_conn.AppendToStream(stream, ExpectedVersion.Any, null, UnknownTypeEvent());
		AppendEvents(2, _conn, stream, 1);

		Start(stream);
		AssertEx.IsOrBecomesTrue(() => Version == 4, TestTimeouts.ThrottleWaitFor, msg: $"Expected 4 got {Version}");
		Assert.Equal(4, Version);
		Assert.Equal(4, Sum);
	}

	public long Sum { get; private set; }
	void IHandle<ReadModelTestEvent>.Handle(ReadModelTestEvent @event) {
		Sum += @event.Value;
	}
	public record ReadModelTestEvent(int Number, int Value) : Event;
	public class ReadModelTestCategoryAggregate : EventDrivenStateMachine;

	private static EventData UnknownTypeEvent() {
		var metadata = Encoding.UTF8.GetBytes("""{"EventClrQualifiedTypeName":"Nope.Missing,dne-assembly"}""");
		return new EventData(Guid.NewGuid(), "Nope", true, "{}"u8.ToArray(), metadata);
	}
}
