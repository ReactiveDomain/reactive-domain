using System;
using System.Collections.Generic;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using Xunit;

namespace ReactiveDomain.Testing.EventStore {
    public class StreamStoreReadTests : IClassFixture<StreamStoreConnectionFixture> {
        private readonly List<IStreamStoreConnection> _stores = new List<IStreamStoreConnection>();
        private readonly IEventSerializer _serializer = new JsonMessageSerializer();
        private readonly string _streamName;
        private readonly int _lastEvent;

        public StreamStoreReadTests(StreamStoreConnectionFixture fixture) {
            IStreamNameBuilder streamNameBuilder = new PrefixedCamelCaseStreamNameBuilder("UnitTest");
            var mockStreamStore = new MockStreamStoreConnection("Test");
            mockStreamStore.Connect();

            _stores.Add(mockStreamStore);
            _stores.Add(fixture.Connection);

            _streamName = streamNameBuilder.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());
            var eventCount = 10;
            foreach (var store in _stores) {
                AppendEvents(eventCount, store, _streamName);
            }

            _lastEvent = eventCount - 1;
        }
        private void AppendEvents(int numEventsToBeSent, IStreamStoreConnection conn, string streamName) {
            for (int evtNumber = 0; evtNumber < numEventsToBeSent; evtNumber++) {
                var evt = new ReadTestTestEvent(evtNumber);
                conn.AppendToStream(streamName, ExpectedVersion.Any, null, _serializer.Serialize(evt));
            }
        }
        [Fact]
        public void connection_name_is_set() {
            var name = "FooConnection";
            var conn = new MockStreamStoreConnection(name);
            Assert.Equal(name, conn.ConnectionName);
        }
        [Fact]
        public void can_create_then_delete_stream() {

            foreach (var conn in _stores) {
                var streamName = $"ReadTest-{Guid.NewGuid()}";
                conn.AppendToStream(streamName, ExpectedVersion.Any, null, _serializer.Serialize(new ReadTestTestEvent(1)));
                var slice = conn.ReadStreamForward(streamName, 0, 1);

                // Ensure stream has been created
                Assert.IsNotType<StreamNotFoundSlice>(slice);
                Assert.IsNotType<StreamDeletedSlice>(slice);

                conn.DeleteStream(streamName, ExpectedVersion.Any);

                // Ensure stream has been deleted
                // We do not support soft delete so StreamNotFoundSlice instead of StreamDeletedSlice
                Assert.IsType<StreamNotFoundSlice>(conn.ReadStreamForward(streamName, 0, 1));
            }
        }
        [Fact]
        public void cannot_delete_system_streams() {
            foreach (var conn in _stores) {
                Assert.Throws<AggregateException>(() => conn.DeleteStream("$streams", ExpectedVersion.Any));
            }
        }
        [Fact]
        public void can_delete_missing_streams() {
            foreach (var conn in _stores) {
                var name = $"Missing-{Guid.NewGuid()}";
                conn.DeleteStream(name, ExpectedVersion.Any);
                conn.DeleteStream(name, ExpectedVersion.EmptyStream);
                conn.DeleteStream(name, ExpectedVersion.NoStream);

                Assert.Throws<ArgumentOutOfRangeException>(
                    ()=>conn.DeleteStream(name, ExpectedVersion.StreamExists)
                    );
            }
        }

        [Fact]
        public void can_read_stream_forward() {


            foreach (var conn in _stores) {

                //before the beginning 
                var startFrom = -3;
                var count = 4;
                Assert.Throws<ArgumentOutOfRangeException>(() => conn.ReadStreamForward(
                                                                        _streamName,
                                                                        startFrom,
                                                                        count));

                //from the beginning
                startFrom = StreamPosition.Start;  // Start == 0
                var slice = conn.ReadStreamForward(
                    _streamName,
                    startFrom,
                    count);

                Assert.True(count == slice.Events.Length, "Failed to read events forward");
                Assert.Equal(startFrom, slice.FromEventNumber);
                Assert.Equal(_lastEvent, slice.LastEventNumber);
                Assert.Equal(startFrom + count, slice.NextEventNumber);
                Assert.False(slice.IsEndOfStream);
                Assert.Equal(ReadDirection.Forward, slice.ReadDirection);
                Assert.True(string.CompareOrdinal(_streamName, slice.Stream) == 0);
                var j = startFrom;
                for (long i = 0; i < count; i++) {
                    var evt = (ReadTestTestEvent)_serializer.Deserialize(slice.Events[i]);
                    Assert.True(j == evt.MessageNumber, $"Expected {j} got {evt.MessageNumber}");
                    j++;
                }

                //from the middle
                startFrom = 3;
                slice = conn.ReadStreamForward(
                    _streamName,
                    startFrom,
                    count);

                Assert.True(count == slice.Events.Length, "Failed to read events forward");
                Assert.Equal(startFrom, slice.FromEventNumber);
                Assert.Equal(_lastEvent, slice.LastEventNumber);
                Assert.Equal(startFrom + count, slice.NextEventNumber);
                Assert.False(slice.IsEndOfStream);
                Assert.Equal(ReadDirection.Forward, slice.ReadDirection);
                Assert.True(string.CompareOrdinal(_streamName, slice.Stream) == 0);
                j = startFrom;
                for (long i = 0; i < count; i++) {
                    var evt = (ReadTestTestEvent)_serializer.Deserialize(slice.Events[i]);
                    Assert.True(j == evt.MessageNumber, $"Expected {j} got {evt.MessageNumber}");
                    j++;
                }
                //to the end
                startFrom = 6;
                slice = conn.ReadStreamForward(
                    _streamName,
                    startFrom,
                    count);

                Assert.True(count == slice.Events.Length, "Failed to read events forward");
                Assert.Equal(startFrom, slice.FromEventNumber);
                Assert.Equal(_lastEvent, slice.LastEventNumber);
                Assert.Equal(startFrom + count, slice.NextEventNumber);
                Assert.True(slice.IsEndOfStream);
                Assert.Equal(ReadDirection.Forward, slice.ReadDirection);
                Assert.True(string.CompareOrdinal(_streamName, slice.Stream) == 0);
                j = startFrom;
                for (long i = 0; i < count; i++) {
                    var evt = (ReadTestTestEvent)_serializer.Deserialize(slice.Events[i]);
                    Assert.True(j == evt.MessageNumber, $"Expected {j} got {evt.MessageNumber}");
                    j++;
                }
                //read past the end
                startFrom = 8;
                slice = conn.ReadStreamForward(
                    _streamName,
                    startFrom,
                    count);
                var expectedCount = 2;
                Assert.True(expectedCount == slice.Events.Length, "Failed to read events forward");
                Assert.Equal(startFrom, slice.FromEventNumber);
                Assert.Equal(_lastEvent, slice.LastEventNumber);
                Assert.Equal(_lastEvent + 1, slice.NextEventNumber);
                Assert.True(slice.IsEndOfStream);
                Assert.Equal(ReadDirection.Forward, slice.ReadDirection);
                Assert.True(string.CompareOrdinal(_streamName, slice.Stream) == 0);
                j = startFrom;
                for (long i = 0; i < expectedCount; i++) {
                    var evt = (ReadTestTestEvent)_serializer.Deserialize(slice.Events[i]);
                    Assert.True(j == evt.MessageNumber, $"Expected {j} got {evt.MessageNumber}");
                    j++;
                }

                //start past the end
                startFrom = 12;
                slice = conn.ReadStreamForward(
                    _streamName,
                    startFrom,
                    count);
                expectedCount = 0;
                Assert.True(expectedCount == slice.Events.Length, "Failed to read events forward");
                Assert.Equal(startFrom, slice.FromEventNumber);
                Assert.Equal(_lastEvent, slice.LastEventNumber);
                Assert.Equal(_lastEvent + 1, slice.NextEventNumber);
                Assert.True(slice.IsEndOfStream);
                Assert.Equal(ReadDirection.Forward, slice.ReadDirection);
                Assert.True(string.CompareOrdinal(_streamName, slice.Stream) == 0);
            }
        }

        [Fact]
        public void can_read_stream_backward() {
            foreach (var conn in _stores) {
                //before the beginning 
                var startFrom = -3;
                var count = 4;
                Assert.Throws<ArgumentOutOfRangeException>(() => conn.ReadStreamBackward(
                                                                        _streamName,
                                                                        startFrom,
                                                                        count));
                //from start past beginning 
                startFrom = StreamPosition.Start;  // Start == 0
                var slice = conn.ReadStreamBackward(
                    _streamName,
                    startFrom,
                    count);
                var expectedCount = 1;
                Assert.True(expectedCount == slice.Events.Length, "Failed to read events forward");
                Assert.Equal(startFrom, slice.FromEventNumber);
                Assert.Equal(_lastEvent, slice.LastEventNumber);
                Assert.Equal(StreamPosition.End, slice.NextEventNumber);
                Assert.True(slice.IsEndOfStream);
                Assert.Equal(ReadDirection.Backward, slice.ReadDirection);
                Assert.True(string.CompareOrdinal(_streamName, slice.Stream) == 0);
                var j = startFrom;
                for (long i = 0; i < expectedCount; i++) {
                    var evt = (ReadTestTestEvent)_serializer.Deserialize(slice.Events[i]);
                    Assert.True(j == evt.MessageNumber, $"Expected {j} got {evt.MessageNumber}");
                    j--;
                }


                //from the middle to the beginning
                startFrom = 4;
                slice = conn.ReadStreamBackward(
                    _streamName,
                    startFrom,
                    count);

                Assert.True(count == slice.Events.Length, "Failed to read events forward");
                Assert.Equal(startFrom, slice.FromEventNumber);
                Assert.Equal(_lastEvent, slice.LastEventNumber);
                Assert.Equal(StreamPosition.Start, slice.NextEventNumber);
                Assert.False(slice.IsEndOfStream);
                Assert.Equal(ReadDirection.Backward, slice.ReadDirection);
                Assert.True(string.CompareOrdinal(_streamName, slice.Stream) == 0);
                j = startFrom;
                for (long i = 0; i < count; i++) {
                    var evt = (ReadTestTestEvent)_serializer.Deserialize(slice.Events[i]);
                    Assert.True(j == evt.MessageNumber, $"Expected {j} got {evt.MessageNumber}");
                    j--;
                }

                //from the middle
                startFrom = 6;
                slice = conn.ReadStreamBackward(
                    _streamName,
                    startFrom,
                    count);

                Assert.True(count == slice.Events.Length, "Failed to read events forward");
                Assert.Equal(startFrom, slice.FromEventNumber);
                Assert.Equal(_lastEvent, slice.LastEventNumber);
                Assert.Equal(startFrom - count, slice.NextEventNumber);
                Assert.False(slice.IsEndOfStream);
                Assert.Equal(ReadDirection.Backward, slice.ReadDirection);
                Assert.True(string.CompareOrdinal(_streamName, slice.Stream) == 0);
                j = startFrom;
                for (long i = 0; i < count; i++) {
                    var evt = (ReadTestTestEvent)_serializer.Deserialize(slice.Events[i]);
                    Assert.True(j == evt.MessageNumber, $"Expected {j} got {evt.MessageNumber}");
                    j--;
                }
                //start past the end and into events
                startFrom = 11;
                slice = conn.ReadStreamBackward(
                    _streamName,
                    startFrom,
                    count);
                expectedCount = 2;
                Assert.True(expectedCount == slice.Events.Length, "Failed to read events backward");
                Assert.Equal(startFrom, slice.FromEventNumber);
                Assert.Equal(_lastEvent, slice.LastEventNumber);
                Assert.Equal(startFrom - count, slice.NextEventNumber);
                Assert.False(slice.IsEndOfStream);
                Assert.Equal(ReadDirection.Backward, slice.ReadDirection);
                Assert.True(string.CompareOrdinal(_streamName, slice.Stream) == 0);
                j = _lastEvent;
                for (long i = 0; i < expectedCount; i++) {
                    var evt = (ReadTestTestEvent)_serializer.Deserialize(slice.Events[i]);
                    Assert.True(j == evt.MessageNumber, $"Expected {j} got {evt.MessageNumber}");
                    j--;
                }

                //start past the end beyond the events
                startFrom = 16;
                slice = conn.ReadStreamBackward(
                    _streamName,
                    startFrom,
                    count);
                expectedCount = 0;
                Assert.True(expectedCount == slice.Events.Length, "Failed to read events backward");
                Assert.Equal(startFrom, slice.FromEventNumber);
                Assert.Equal(_lastEvent, slice.LastEventNumber);
                Assert.Equal(_lastEvent, slice.NextEventNumber);
                Assert.False(slice.IsEndOfStream);
                Assert.Equal(ReadDirection.Backward, slice.ReadDirection);
                Assert.True(string.CompareOrdinal(_streamName, slice.Stream) == 0);
            }
        }
        public class ReadTestTestEvent : Event {
            public readonly int MessageNumber;
            public ReadTestTestEvent(
                int messageNumber
            ) : base(NewRoot()) {
                MessageNumber = messageNumber;
            }
        }
    }

}
