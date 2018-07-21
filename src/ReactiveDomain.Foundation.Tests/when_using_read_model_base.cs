using System;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests {
    // ReSharper disable once InconsistentNaming
    public class when_using_read_model_base :
                    ReadModelBase,
                    IHandle<when_using_read_model_base.ReadModelTestEvent>,
                    IClassFixture<StreamStoreConnectionFixture> {

        private static IListener GetListener() {
            return new SynchronizableStreamListener(
                        nameof(when_using_read_model_base),
                        _conn,
                        Namer,
                        Serializer,
                        true);
        }

        private static IStreamStoreConnection _conn;
        private static readonly IEventSerializer Serializer =
            new JsonMessageSerializer();
        private static readonly IStreamNameBuilder Namer =
            new PrefixedCamelCaseStreamNameBuilder(nameof(when_using_read_model_base));

        private readonly string _stream1;
        private readonly string _stream2;


        public when_using_read_model_base(StreamStoreConnectionFixture fixture)
                    : base(nameof(when_using_read_model_base), GetListener) {
            //_conn = new MockStreamStoreConnection("mockStore");
            _conn = fixture.Connection;
            _conn.Connect();

            // ReSharper disable once RedundantTypeArgumentsOfMethod
            EventStream.Subscribe<ReadModelTestEvent>(this);

            _stream1 = Namer.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());
            _stream2 = Namer.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());

            AppendEvents(10, _conn, _stream1, 2);
            AppendEvents(10, _conn, _stream2, 3);
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
            AssertEx.IsOrBecomesTrue(() => Count == 1, 1000, msg: $"Expected 1 got {Count}");
            AssertEx.IsOrBecomesTrue(() => Sum == 7);
        }
        [Fact]
        public void can_start_streams_by_aggregate_category() {
            
            var s1 = Namer.GenerateForAggregate(typeof(ReadModelTestCategoryAggregate), Guid.NewGuid());
            AppendEvents(1, _conn, s1, 7);
            var s2 = Namer.GenerateForAggregate(typeof(ReadModelTestCategoryAggregate), Guid.NewGuid());
            AppendEvents(1, _conn, s2, 5);
            Start<ReadModelTestCategoryAggregate>(null,true);

            AssertEx.IsOrBecomesTrue(() => Count == 2, 1000, msg: $"Expected 2 got {Count}");
            AssertEx.IsOrBecomesTrue(() => Sum == 12);
        }
        [Fact]
        public void can_read_one_stream() {
            Start(_stream1);
            AssertEx.IsOrBecomesTrue(() => Count == 10, 1000, msg: $"Expected 10 got {Count}");
            AssertEx.IsOrBecomesTrue(() => Sum == 20);
            //confirm checkpoints
            Assert.Equal(_stream1, GetCheckpoint()[0].Item1);
            Assert.Equal(9, GetCheckpoint()[0].Item2);
        }
        [Fact]
        public void can_read_two_streams() {
            Start(_stream1);
            Start(_stream2);
            AssertEx.IsOrBecomesTrue(() => Count == 20, 1000, msg: $"Expected 20 got {Count}");
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
            AssertEx.IsOrBecomesTrue(() => Count == 10, 100, msg: $"Expected 10 got {Count}");
            AssertEx.IsOrBecomesTrue(() => Sum == 20, 100);
        }
        [Fact]
        public void can_wait_for_two_streams_to_go_live() {
            Start(_stream1, null, true);
            AssertEx.IsOrBecomesTrue(() => Count == 10, 100, msg: $"Expected 10 got {Count}");
            AssertEx.IsOrBecomesTrue(() => Sum == 20, 100);

            Start(_stream2, null, true);
            AssertEx.IsOrBecomesTrue(() => Count == 20, 10, msg: $"Expected 20 got {Count}");
            AssertEx.IsOrBecomesTrue(() => Sum == 50, 100);
        }
        [Fact]
        public void can_listen_to_one_stream() {
            Start(_stream1);
            AssertEx.IsOrBecomesTrue(() => Count == 10, 1000, msg: $"Expected 10 got {Count}");
            AssertEx.IsOrBecomesTrue(() => Sum == 20);
            //add more messages
            AppendEvents(10, _conn, _stream1, 5);
            AssertEx.IsOrBecomesTrue(() => Count == 20, 1000, msg: $"Expected 20 got {Count}");
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
            AssertEx.IsOrBecomesTrue(() => Count == 20, 1000, msg: $"Expected 20 got {Count}");
            AssertEx.IsOrBecomesTrue(() => Sum == 50);
            //add more messages
            AppendEvents(10, _conn, _stream1, 5);
            AppendEvents(10, _conn, _stream2, 7);
            AssertEx.IsOrBecomesTrue(() => Count == 40, 1000, msg: $"Expected 20 got {Count}");
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
            Count = 9;
            Sum = 18;
            //start at the checkpoint
            Start(_stream1, checkPoint);
            //add the one recorded event
            AssertEx.IsOrBecomesTrue(() => Count == 10, 100, msg: $"Expected 10 got {Count}");
            AssertEx.IsOrBecomesTrue(() => Sum == 20);
            //add more messages
            AppendEvents(10, _conn, _stream1, 5);
            AssertEx.IsOrBecomesTrue(() => Count == 20, 100, msg: $"Expected 20 got {Count}");
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
            Count = (9) + (6);
            Sum = (9 * 2) + (6 * 3);
            Start(_stream1, checkPoint1);
            Start(_stream2, checkPoint2);
            //add the recorded events 2 on stream 1 & 5 on stream 2
            AssertEx.IsOrBecomesTrue(() => Count == 20, 1000, msg: $"Expected 20 got {Count}");
            AssertEx.IsOrBecomesTrue(() => Sum == 50, msg: $"Expected 50 got {Sum}");
            //add more messages
            AppendEvents(10, _conn, _stream1, 5);
            AppendEvents(10, _conn, _stream2, 7);
            AssertEx.IsOrBecomesTrue(() => Count == 40, 1000, msg: $"Expected 20 got {Count}");
            AssertEx.IsOrBecomesTrue(() => Sum == 170);
            //confirm checkpoints
            Assert.Equal(_stream1, GetCheckpoint()[0].Item1);
            Assert.Equal(19, GetCheckpoint()[0].Item2);
            Assert.Equal(_stream2, GetCheckpoint()[1].Item1);
            Assert.Equal(19, GetCheckpoint()[1].Item2);
        }
        [Fact]
        public void can_listen_to_the_same_stream_twice() {
            Assert.Equal(0,Count);
            //weird but true
            //n.b. Don't do this on purpose
            Start(_stream1);
            Start(_stream1);
            //double events
            AssertEx.IsOrBecomesTrue(() => Count == 20, 1000, msg: $"Expected 20 got {Count}");
            AssertEx.IsOrBecomesTrue(() => Sum == 40);
            //even more doubled events
            AppendEvents(10, _conn, _stream1, 5);
            AssertEx.IsOrBecomesTrue(() => Count == 40, 2000, msg: $"Expected 40 got {Count}");
            AssertEx.IsOrBecomesTrue(() => Sum == 140);
        }

        public long Sum { get; private set; }
        public long Count { get; private set; }
        void IHandle<ReadModelTestEvent>.Handle(ReadModelTestEvent @event) {
            Sum += @event.Value;
            Count++;
        }
        public class ReadModelTestEvent : Message {
            public readonly int Number;
            public readonly int Value;

            public ReadModelTestEvent(
                int number,
                int value
                ) {
                Number = number;
                Value = value;
            }
        }
        public class ReadModelTestCategoryAggregate:EventDrivenStateMachine{}
    }
}
