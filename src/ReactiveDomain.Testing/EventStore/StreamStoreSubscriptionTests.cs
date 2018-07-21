using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using Xunit;

namespace ReactiveDomain.Testing.EventStore {

    public class StreamStoreSubscriptionTests : IClassFixture<StreamStoreConnectionFixture> {
        private readonly List<IStreamStoreConnection> _stores = new List<IStreamStoreConnection>();
        private readonly IStreamNameBuilder _streamNameBuilder;
        private readonly IEventSerializer _serializer = new JsonMessageSerializer();
        private readonly string _streamName;
        private readonly UserCredentials _admin;
        private const long True = 1;
        private const long False = 0;

        public StreamStoreSubscriptionTests(StreamStoreConnectionFixture fixture) {
            _admin = fixture.AdminCredentials;
            _streamNameBuilder = new PrefixedCamelCaseStreamNameBuilder("UnitTest");
            var mockStreamStore = new MockStreamStoreConnection(nameof(MockStreamStoreConnection));
            mockStreamStore.Connect();
            fixture.Connection.Connect();
            _stores.Add(mockStreamStore);
            _stores.Add(fixture.Connection);

            _streamName = _streamNameBuilder.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());
            var eventCount = 10;
            foreach (var store in _stores) {
                AppendEvents(eventCount, store, _streamName);
            }


        }
        private void AppendEvents(int numEventsToBeSent, IStreamStoreConnection conn, string streamName, int startNumber = 0) {
            for (int evtNumber = startNumber; evtNumber < numEventsToBeSent + startNumber; evtNumber++) {
                var evt = new SubscriptionTestEvent(evtNumber);
                conn.AppendToStream(streamName, ExpectedVersion.Any, null, _serializer.Serialize(evt));
            }
        }


        [Fact]
        public void can_subscribe_to_stream() {

            foreach (var conn in _stores) {

                var dropped = False;
                long evtCount = 0;
                long evtNumber = 0;
                var sub = conn.SubscribeToStream(
                                        _streamName,
                                        evt => {
                                            var subEvent = (SubscriptionTestEvent)_serializer.Deserialize(evt);
                                            Interlocked.Increment(ref evtCount);
                                            Interlocked.Exchange(ref evtNumber, subEvent.MessageNumber);
                                        },
                                        (reason, ex) => Interlocked.Exchange(ref dropped, True));

                Assert.Equal(0, evtCount);
                Assert.Equal(0, evtNumber);

                AppendEvents(1, conn, _streamName, 11);

                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref evtCount) == 1);
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref evtNumber) == 11);
                AppendEvents(5, conn, _streamName, 12);

                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref evtCount) == 6);
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref evtNumber) == 16);

                sub.Dispose();
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref dropped) == True, msg: "Failed to handle drop");
            }
        }

        [Fact]
        public void can_subscribe_to_stream_from() {

            var streamName = _streamNameBuilder.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());

            foreach (var conn in _stores) {
                AppendEvents(5, conn, streamName);

                long evtCount = 0;
                var dropped = false;
                var liveProcessingStarted = false;

                var sub = conn.SubscribeToStreamFrom(
                                            streamName,
                                            2,//after the third event of 5
                                            CatchUpSubscriptionSettings.Default,
                                            // ReSharper disable once AccessToModifiedClosure
                                            evt => Interlocked.Increment(ref evtCount),
                                            _ => liveProcessingStarted = true,
                                            (reason, ex) => dropped = true);


                AssertEx.IsOrBecomesTrue(() => liveProcessingStarted, 2000, msg: "Failed handle live processing start");
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref evtCount) == 2, 5000, msg: $"Expected 2 Events got { Interlocked.Read(ref evtCount)}");
                Task.Run(()=> AppendEvents(5, conn, streamName));
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref evtCount) == 7, 5000, msg: $"Expected 7 Events got { Interlocked.Read(ref evtCount)}");
                AppendEvents(5, conn, streamName);
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref evtCount) == 12, 5000, msg: $"Expected 12 Events got { Interlocked.Read(ref evtCount)}");
                sub.Dispose();
                AssertEx.IsOrBecomesTrue(() => dropped, msg: "Failed to handle drop");
            }
        }

        [Fact]
        public void can_subscribe_to_all() {

            var streams = new List<string>
            {
                _streamNameBuilder.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid()),
                _streamNameBuilder.GenerateForAggregate(typeof(TestWoftamAggregate), Guid.NewGuid())
            };

            foreach (var conn in _stores) {


                long evtCount = 0;
                var dropped = false;


                //first event in a stream is copied to the $streams projection in the in-Memory ES
                //the Mock ES does not support this projection
                foreach (var stream in streams) {
                    var evt = new StreamCreatedTestEvent();
                    conn.AppendToStream(stream, ExpectedVersion.Any, null, _serializer.Serialize(evt));
                }

                var sub = conn.SubscribeToAll(
                                        evt => {
                                            if (string.CompareOrdinal(evt.EventType, nameof(SubscriptionTestEvent)) == 0) {
                                                Interlocked.Increment(ref evtCount);
                                            }
                                        },
                                        (reason, ex) => dropped = true,
                                        _admin);
                foreach (var stream in streams) {
                    AppendEvents(5, conn, stream);
                }
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref evtCount) == 30, 2000);

                sub.Dispose();
                AssertEx.IsOrBecomesTrue(() => dropped, msg: "Failed to handle drop");
            }
        }
        [Fact]
        public void can_subscribe_to_event_type_stream() {

            var streamTypeName = _streamNameBuilder.GenerateForEventType(typeof(SubscriptionTestEvent).Name);
            var streams = new List<string>
            {
                _streamNameBuilder.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid()),
                _streamNameBuilder.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid())
            };

            foreach (var conn in _stores) {
                long evtCount = 0;
                var dropped = false;

                var sub = conn.SubscribeToStream(
                                    streamTypeName,
                                    // ReSharper disable once AccessToModifiedClosure
                                    evt => Interlocked.Increment(ref evtCount),
                                    (reason, ex) => dropped = true,
                                    _admin);

                foreach (var stream in streams) {
                    AppendEvents(5, conn, stream);
                }
                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref evtCount) == 10, 2000, $"Expected 10 got {Interlocked.Read(ref evtCount)}");
                sub.Dispose();
                AssertEx.IsOrBecomesTrue(() => dropped, msg: "Failed to handle drop");
            }
        }
        public class STestCategoryAggregate : EventDrivenStateMachine { }
        [Fact]
        public void can_subscribe_to_category_stream() {
            var streamTypeName = _streamNameBuilder.GenerateForCategory(typeof(STestCategoryAggregate));
            var streams = new []
            {
                _streamNameBuilder.GenerateForAggregate(typeof(STestCategoryAggregate), Guid.NewGuid()),
                _streamNameBuilder.GenerateForAggregate(typeof(STestCategoryAggregate), Guid.NewGuid())
            };

            foreach (var conn in _stores) {
                long evtCount = 0;
                var dropped = false;
                foreach (var stream in streams) {
                    AppendEvents(1, conn, stream);
                }
                Thread.Sleep(250);
                var sub = conn.SubscribeToStream(
                    streamTypeName,
                    // ReSharper disable once AccessToModifiedClosure
                    evt => Interlocked.Increment(ref evtCount),
                    (reason, ex) => dropped = true,
                    _admin);


                AppendEvents(5, conn, streams[0]);
                AppendEvents(5, conn, streams[1]);

                AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref evtCount) == 10, 2000,
                    $"Expected 10 got {Interlocked.Read(ref evtCount)} on {conn.ConnectionName}");
                sub.Dispose();
                AssertEx.IsOrBecomesTrue(() => dropped, msg: "Failed to handle drop");
            }
        }
        public class StreamCreatedTestEvent : Event {
            public StreamCreatedTestEvent() : base(NewRoot()) {

            }
        }
        public class SubscriptionTestEvent : Event {
            public readonly int MessageNumber;
            public SubscriptionTestEvent(
                int messageNumber
            ) : base(NewRoot()) {
                MessageNumber = messageNumber;
            }
        }
    }
}
