using System;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests;

// ReSharper disable once InconsistentNaming
public class when_using_read_model_base :
    ReadModelBase,
    IHandle<when_using_read_model_base.ReadModelTestEvent>,
    IClassFixture<StreamStoreConnectionFixture> {

    private static IStreamStoreConnection _conn;
    private static readonly IEventSerializer Serializer =
        new JsonMessageSerializer();
    private static readonly IStreamNameBuilder Namer =
        new PrefixedCamelCaseStreamNameBuilder(nameof(when_using_read_model_base));

    private readonly string _stream1;
    private readonly string _stream2;


    public when_using_read_model_base(StreamStoreConnectionFixture fixture)
        : base(nameof(when_using_read_model_base), new ConfiguredConnection(fixture.Connection, Namer, Serializer)) {
        _conn = fixture.Connection;
        _conn.Connect();

        // ReSharper disable once RedundantTypeArgumentsOfMethod
        EventStream.Subscribe<ReadModelTestEvent>(this);

        _stream1 = Namer.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());
        _stream2 = Namer.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());

        AppendEvents(10, _conn, _stream1, 2);
        AppendEvents(10, _conn, _stream2, 3);

        _conn.TryConfirmStream(_stream1, 10);
        _conn.TryConfirmStream(_stream2, 10);
        _conn.TryConfirmStream(Namer.GenerateForCategory(typeof(TestAggregate)), 20);
    }

    private void AppendEvents(
        int numEventsToBeSent,
        IStreamStoreConnection conn,
        string streamName,
        int value) {
        for (int evtNumber = 0; evtNumber < numEventsToBeSent; evtNumber++) {
            var evt = new ReadModelTestEvent(evtNumber, value);
            conn.AppendToStream(streamName, ExpectedVersion.Any, null, Serializer.Serialize(evt));
        }
    }
    [Fact]
    public void can_start_streams_by_aggregate() {
        var aggId = Guid.NewGuid();
        var s1 = Namer.GenerateForAggregate(typeof(TestAggregate), aggId);
        AppendEvents(1, _conn, s1, 7);
        Start<TestAggregate>(aggId);
        AssertEx.AtLeastModelVersion(this, 2, msg: $"Expected 2 got {Version}"); // 1 message + CatchupSubscriptionBecameLive
        AssertEx.IsOrBecomesTrue(() => Sum == 7);
    }
    [Fact]
    public void can_start_streams_by_aggregate_category() {

        var s1 = Namer.GenerateForAggregate(typeof(ReadModelTestCategoryAggregate), Guid.NewGuid());
        AppendEvents(1, _conn, s1, 7);
        var s2 = Namer.GenerateForAggregate(typeof(ReadModelTestCategoryAggregate), Guid.NewGuid());
        AppendEvents(1, _conn, s2, 5);
        Start<ReadModelTestCategoryAggregate>(null, true);

        AssertEx.AtLeastModelVersion(this, 3, msg: $"Expected 3 got {Version}");
        AssertEx.IsOrBecomesTrue(() => Sum == 12);
    }
    [Fact]
    public void can_read_one_stream() {
        Start(_stream1);
        AssertEx.AtLeastModelVersion(this, 11, msg: $"Expected 11 got {Version}");
        AssertEx.IsOrBecomesTrue(() => Sum == 20);
        //confirm checkpoints
        Assert.Equal(_stream1, GetCheckpoint()[0].Item1);
        Assert.Equal(9, GetCheckpoint()[0].Item2);
    }
    [Fact]
    public void can_read_two_streams() {
        Start(_stream1);
        Start(_stream2);
        AssertEx.AtLeastModelVersion(this, 22, msg: $"Expected 22 got {Version}");
        AssertEx.IsOrBecomesTrue(() => Sum == 50);
        //confirm checkpoints
        Assert.Equal(_stream1, GetCheckpoint()[0].Item1);
        Assert.Equal(9, GetCheckpoint()[0].Item2);
        Assert.Equal(_stream2, GetCheckpoint()[1].Item1);
        Assert.Equal(9, GetCheckpoint()[1].Item2);
    }
    [Fact]
    public void can_wait_for_one_stream_to_go_live() {
        Start(_stream1, null, true);
        AssertEx.AtLeastModelVersion(this, 11, TimeSpan.FromMilliseconds(100), msg: $"Expected 11 got {Version}");
        AssertEx.IsOrBecomesTrue(() => Sum == 20, 100);
    }
    [Fact]
    public void can_wait_for_two_streams_to_go_live() {
        Start(_stream1, null, true);
        AssertEx.AtLeastModelVersion(this, 11, TimeSpan.FromMilliseconds(100), msg: $"Expected 11 got {Version}");
        AssertEx.IsOrBecomesTrue(() => Sum == 20, 150);

        Start(_stream2, null, true);
        AssertEx.AtLeastModelVersion(this, 21, TimeSpan.FromMilliseconds(100), msg: $"Expected 21 got {Version}");
        AssertEx.IsOrBecomesTrue(() => Sum == 50, 150);
    }
    [Fact]
    public void can_listen_to_one_stream() {
        Start(_stream1);
        AssertEx.AtLeastModelVersion(this, 11, msg: $"Expected 11 got {Version}");
        AssertEx.IsOrBecomesTrue(() => Sum == 20);
        //add more messages
        AppendEvents(10, _conn, _stream1, 5);
        AssertEx.AtLeastModelVersion(this, 21, msg: $"Expected 21 got {Version}");
        AssertEx.IsOrBecomesTrue(() => Sum == 70);
        //confirm checkpoints
        Assert.Equal(_stream1, GetCheckpoint()[0].Item1);
        Assert.Equal(19, GetCheckpoint()[0].Item2); Assert.Equal(_stream1, GetCheckpoint()[0].Item1);
        Assert.Equal(19, GetCheckpoint()[0].Item2);
    }
    [Fact]
    public void can_listen_to_two_streams() {
        Start(_stream1);
        Start(_stream2);
        AssertEx.AtLeastModelVersion(this, 22, msg: $"Expected 22 got {Version}");
        AssertEx.IsOrBecomesTrue(() => Sum == 50);
        //add more messages
        AppendEvents(10, _conn, _stream1, 5);
        AppendEvents(10, _conn, _stream2, 7);
        AssertEx.AtLeastModelVersion(this, 42, msg: $"Expected 42 got {Version}");
        AssertEx.IsOrBecomesTrue(() => Sum == 170);
        //confirm checkpoints
        Assert.Equal(_stream1, GetCheckpoint()[0].Item1);
        Assert.Equal(19, GetCheckpoint()[0].Item2);
        Assert.Equal(_stream2, GetCheckpoint()[1].Item1);
        Assert.Equal(19, GetCheckpoint()[1].Item2);
    }
    [Fact]
    public void can_use_checkpoint_on_one_stream() {
        //restore state
        var checkPoint = 8L;//Zero based, ignore the first 9 events
        Sum = 18;
        //start at the checkpoint
        Start(_stream1, checkPoint);
        //add the one recorded event
        AssertEx.AtLeastModelVersion(this, 2, TimeSpan.FromMilliseconds(100), msg: $"Expected 2 got {Version}");
        AssertEx.IsOrBecomesTrue(() => Sum == 20);
        //add more messages
        AppendEvents(10, _conn, _stream1, 5);
        AssertEx.AtLeastModelVersion(this, 3, TimeSpan.FromMilliseconds(200), msg: $"Expected 3 got {Version}");
        AssertEx.AtLeastModelVersion(this, 8, TimeSpan.FromMilliseconds(100), msg: $"Expected 8 got {Version}");
        AssertEx.AtLeastModelVersion(this, 12, TimeSpan.FromMilliseconds(100), msg: $"Expected 12 got {Version}");
        AssertEx.IsOrBecomesTrue(() => Sum == 70);
        //confirm checkpoints
        Assert.Equal(_stream1, GetCheckpoint()[0].Item1);
        Assert.Equal(19, GetCheckpoint()[0].Item2);
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
        AssertEx.AtLeastModelVersion(this, 7, msg: $"Expected 7 got {Version}");
        AssertEx.IsOrBecomesTrue(() => Sum == 50, msg: $"Expected 50 got {Sum}");
        //add more messages
        AppendEvents(10, _conn, _stream1, 5);
        AppendEvents(10, _conn, _stream2, 7);
        AssertEx.AtLeastModelVersion(this, 27, msg: $"Expected 27 got {Version}");
        AssertEx.IsOrBecomesTrue(() => Sum == 170);
        //confirm checkpoints
        Assert.Equal(_stream1, GetCheckpoint()[0].Item1);
        Assert.Equal(19, GetCheckpoint()[0].Item2);
        Assert.Equal(_stream2, GetCheckpoint()[1].Item1);
        Assert.Equal(19, GetCheckpoint()[1].Item2);
    }
    [Fact]
    public void can_listen_to_the_same_stream_twice() {
        Assert.Equal(0, Version);
        //weird but true
        //n.b. Don't do this on purpose
        Start(_stream1);
        Start(_stream1);
        //double events
        AssertEx.AtLeastModelVersion(this, 22, msg: $"Expected 22 got {Version}");
        AssertEx.IsOrBecomesTrue(() => Sum == 40);
        //even more doubled events
        AppendEvents(10, _conn, _stream1, 5);
        AssertEx.AtLeastModelVersion(this, 42, TimeSpan.FromSeconds(2), msg: $"Expected 42 got {Version}");
        AssertEx.IsOrBecomesTrue(() => Sum == 140);
    }

    public long Sum { get; private set; }
    void IHandle<ReadModelTestEvent>.Handle(ReadModelTestEvent @event) {
        Sum += @event.Value;
    }
    public record ReadModelTestEvent(int Number, int Value) : Event;
    public class ReadModelTestCategoryAggregate : EventDrivenStateMachine;
}