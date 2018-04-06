using ReactiveDomain.Foundation;
using ReactiveDomain.Foundation.EventStore;
using ReactiveDomain.Util;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ReactiveDomain.Testing
{
    // todo: separate stream connection tests and repo tests
    // ReSharper disable InconsistentNaming
    public class MockStreamStoreConnectionTests : IClassFixture<StreamStoreConnectionFixture> {
        private readonly List<IStreamStoreConnection> _streamStoreConnections = new List<IStreamStoreConnection>();

        private readonly List<IRepository> _repos = new List<IRepository>();
        private readonly IStreamNameBuilder _streamNameBuilder;
        private readonly UserCredentials _admin;

        public MockStreamStoreConnectionTests(StreamStoreConnectionFixture fixture)
        {
            _admin = fixture.AdminCredentials;
            _streamNameBuilder = new PrefixedCamelCaseStreamNameBuilder("UnitTest");

            // todo: uncomment this and make sure all tests pass
            // _streamStoreConnections.Add(new MockStreamStoreConnection("Test"));
            _streamStoreConnections.Add(fixture.Connection);

            //todo: reconnect bus to the all stream subscription
            // _repos.Add(new StreamStoreRepository(_streamNameBuilder, new MockStreamStoreConnection("Test")));
            _repos.Add(new StreamStoreRepository(_streamNameBuilder, fixture.Connection));
        }

        [Fact]
        public void can_create_then_delete_stream()
        {
            var streamName = _streamNameBuilder.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());
            foreach (var conn in _streamStoreConnections)
            {
                var createEvent = new EventData(Guid.NewGuid(), typeof(TestAggregateMessages.NewAggregate).Name, true, new byte[] { 0 }, new byte[] { 0 });
                conn.AppendToStream(streamName, ExpectedVersion.Any, null, createEvent);
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
        public void can_read_stream_forward()
        {
            var streamName = _streamNameBuilder.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());
            var numEventsToBeSent = 5; // thoughts: think about randomizing those maybe?
            long count = 2;
            long startFrom = numEventsToBeSent - count; // We want to get the <count> last events in the right order (forward)

            foreach (var conn in _streamStoreConnections)
            {
                var expectedEvents = new List<EventData>();
                for (byte eventByteData = 0; eventByteData < numEventsToBeSent; eventByteData++)
                {
                    var eventMetaData = eventByteData;
                    var createEvent = new EventData(Guid.NewGuid(), typeof(TestAggregateMessages.NewAggregate).Name, true, new byte[] { eventByteData }, new byte[] { eventMetaData });
                    conn.AppendToStream(streamName, ExpectedVersion.Any, null, createEvent);
                    if (eventByteData >= startFrom) expectedEvents.Add(createEvent);
                }
                
                var slice = conn.ReadStreamForward(
                                            streamName,
                                            startFrom,
                                            count);

                Assert.IsOrBecomesTrue(() => count == slice.Events.Length, msg: "Failed to read events forward");
                for (int i = 0; i < count; i++)
                {
                    Assert.Equal(expectedEvents[i].EventId, slice.Events[i].EventId);
                    Assert.Equal(expectedEvents[i].Type, slice.Events[i].EventType);
                    Assert.Equal(expectedEvents[i].Data, slice.Events[i].Data);
                    Assert.Equal(expectedEvents[i].Metadata, slice.Events[i].Metadata);
                }
            }
        }

        [Fact]
        public void can_read_stream_backward()
        {
            var streamName = _streamNameBuilder.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());
            var numEventsToBeSent = 5;
            long count = 4;
            long startFrom = count - 1; // We want to get the <count> first events in the right order (backward)

            foreach (var conn in _streamStoreConnections)
            {
                var expectedEvents = new List<EventData>();
                for (byte eventByteData = 0; eventByteData < numEventsToBeSent; eventByteData++)
                {
                    var eventMetaData = eventByteData;
                    var createEvent = new EventData(Guid.NewGuid(), typeof(TestAggregateMessages.NewAggregate).Name, true, new byte[] { eventByteData }, new byte[] { eventMetaData });
                    conn.AppendToStream(streamName, ExpectedVersion.Any, null, createEvent);
                    if (eventByteData <= startFrom) expectedEvents.Add(createEvent);
                }
                expectedEvents.Reverse();

                var slice = conn.ReadStreamBackward(
                                            streamName,
                                            startFrom,
                                            count);

                Assert.IsOrBecomesTrue(() => count == slice.Events.Length, msg: "Failed to read events backward");
                for (int i = 0; i < count; i++)
                {
                    Assert.Equal(expectedEvents[i].EventId, slice.Events[i].EventId);
                    Assert.Equal(expectedEvents[i].Type, slice.Events[i].EventType);
                    Assert.Equal(expectedEvents[i].Data, slice.Events[i].Data);
                    Assert.Equal(expectedEvents[i].Metadata, slice.Events[i].Metadata);
                }
            }
        }

        [Fact]
        public void can_subscribe_to_stream()
        {
            var streamName = _streamNameBuilder.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());
            var numberOfEvent = 2;

            foreach (var conn in _streamStoreConnections)
            {
                var capturedEvents = new List<RecordedEvent>();
                var dropped = false;

                var sub = conn.SubscribeToStream(
                                        streamName,
                                        async evt => { capturedEvents.Add(evt); await Task.FromResult(Unit.Default); },
                                        (reason, ex) => dropped = true);

                var expectedEvents = new List<EventData>();
                for (byte eventByteData = 0; eventByteData < numberOfEvent; eventByteData++)
                {
                    var eventMetaData = eventByteData;
                    var testEvent = new EventData(Guid.NewGuid(), typeof(TestAggregateMessages.NewAggregate).Name, true, new byte[] { eventByteData }, new byte[] { eventMetaData });
                    conn.AppendToStream(streamName, ExpectedVersion.Any, null, testEvent);
                    expectedEvents.Add(testEvent);
                }

                Assert.IsOrBecomesTrue(() => numberOfEvent == capturedEvents.Count, msg: $"Failed to capture events. Expected {numberOfEvent} found {capturedEvents.Count}");
                for (int i = 0; i < numberOfEvent; i++)
                {
                    Assert.Equal(expectedEvents[i].EventId, capturedEvents[i].EventId);
                    Assert.Equal(expectedEvents[i].Type, capturedEvents[i].EventType);
                    Assert.Equal(expectedEvents[i].Data, capturedEvents[i].Data);
                    Assert.Equal(expectedEvents[i].Metadata, capturedEvents[i].Metadata);
                }

                sub.Dispose();
                Assert.IsOrBecomesTrue(() => dropped, msg: "Failed to handle drop");
            }
        }

        [Fact]
        public void can_subscribe_to_stream_from()
        {
            var streamName = _streamNameBuilder.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());
            var numEventsToBeSent = 5;
            var count = 2;
            var lastCheckpoint = numEventsToBeSent - count - 1;  // we want to capture the <count> last events in the right order
            var settings = new CatchUpSubscriptionSettings(numEventsToBeSent, 1, true, streamName);

            foreach (var conn in _streamStoreConnections)
            {
                var capturedEvents = new List<RecordedEvent>();
                var dropped = false;
                var liveProcessingStarted = false;

                var sub = conn.SubscribeToStreamFrom(
                                            streamName,
                                            lastCheckpoint,
                                            settings,
                                            async evt => { capturedEvents.Add(evt); await Task.FromResult(Unit.Default); },
                                            _ => liveProcessingStarted = true,
                                            (reason, ex) => dropped = true);

                Assert.IsOrBecomesTrue(() => liveProcessingStarted, msg: "Failed handle live processing start");

                var expectedEvents = new List<EventData>();
                for (byte eventByteData = 0; eventByteData < numEventsToBeSent; eventByteData++)
                {
                    var eventMetaData = eventByteData;
                    var createEvent = new EventData(Guid.NewGuid(), typeof(TestAggregateMessages.NewAggregate).Name, true, new byte[] { eventByteData }, new byte[] { eventMetaData });
                    conn.AppendToStream(streamName, ExpectedVersion.Any, null, createEvent);
                    if (eventByteData >= numEventsToBeSent - count) expectedEvents.Add(createEvent);
                }

                Assert.IsOrBecomesTrue(() => count == capturedEvents.Count, msg: $"Failed to capture events. Expected {count} found {capturedEvents.Count}");
                for (int i = 0; i < count; i++)
                {
                    Assert.Equal(expectedEvents[i].EventId, capturedEvents[i].EventId);
                    Assert.Equal(expectedEvents[i].Type, capturedEvents[i].EventType);
                    Assert.Equal(expectedEvents[i].Data, capturedEvents[i].Data);
                    Assert.Equal(expectedEvents[i].Metadata, capturedEvents[i].Metadata);
                }

                sub.Dispose();
                Assert.IsOrBecomesTrue(() => dropped, msg: "Failed to handle drop");
            }
        }

        [Fact]
        public void can_subscribe_to_all()
        {
            var numberOfEvent = 2; // We want to make sure we capture the <numberOfEvent> events in each stream in the right order
            var streams = new List<string>
            {
                _streamNameBuilder.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid()),
                _streamNameBuilder.GenerateForAggregate(typeof(TestWoftamAggregate), Guid.NewGuid())
            };

            foreach (var conn in _streamStoreConnections)
            {
                var capturedEvents = new List<RecordedEvent>();
                var dropped = false;

                var sub = conn.SubscribeToAll(
                                        async evt => { capturedEvents.Add(evt); await Task.FromResult(Unit.Default); },
                                        (reason, ex) => dropped = true,
                                        _admin);

                foreach (var stream in streams)
                {
                    var expectedEvents = new List<EventData>();
                    for (byte eventByteData = 0; eventByteData < numberOfEvent; eventByteData++)
                    {
                        var eventMetaData = eventByteData;
                        var createEvent = new EventData(Guid.NewGuid(), typeof(TestAggregateMessages.NewAggregate).Name, true, new byte[] { eventByteData }, new byte[] { eventMetaData });
                        conn.AppendToStream(stream, ExpectedVersion.Any, null, createEvent);
                        expectedEvents.Add(createEvent);
                    }

                    Assert.IsOrBecomesTrue(() => numberOfEvent == capturedEvents.Count, msg: $"Failed to subscribe to events on stream {stream}. Expected {numberOfEvent} found {capturedEvents.Count}");
                    for (int i = 0; i < numberOfEvent; i++)
                    {
                        Assert.Equal(expectedEvents[i].EventId, capturedEvents[i].EventId);
                        Assert.Equal(expectedEvents[i].Type, capturedEvents[i].EventType);
                        Assert.Equal(expectedEvents[i].Data, capturedEvents[i].Data);
                        Assert.Equal(expectedEvents[i].Metadata, capturedEvents[i].Metadata);
                    }

                    capturedEvents.Clear();
                }

                sub.Dispose();
                Assert.IsOrBecomesTrue(() => dropped, msg: "Failed to handle drop");
            }
        }

        [Fact]
        public void can_subscribe_to_category_stream()
        {
            // todo: implement this as replacement to old repo tests
        }

        [Fact]
        public void can_subscribe_to_event_type_stream()
        {
            // todo: implement this as replacement to old repo tests
        }

        [Fact]
        public void can_save_new_aggregate() {
            foreach (var repo in _repos) {
                var id = Guid.NewGuid();
                var tAgg = new TestAggregate(id);
                repo.Save(tAgg);
                var rAgg = repo.GetById<TestAggregate>(id);
                Assert.NotNull(rAgg);
                Assert.Equal(tAgg.Id, rAgg.Id);
            }
        }

        [Fact]
        public void can_update_and_save_aggregate() {
            foreach (var repo in _repos) {
                var id = Guid.NewGuid();
                var tAgg = new TestAggregate(id);
                repo.Save(tAgg);
                // Get v2
                var v2Agg = repo.GetById<TestAggregate>(id);
                Assert.Equal((uint)0, v2Agg.CurrentAmount());
                // update v2
                v2Agg.RaiseBy(1);
                Assert.Equal((uint)1, v2Agg.CurrentAmount());
                repo.Save(v2Agg);
                // get v3
                var v3Agg = repo.GetById<TestAggregate>(id);
                Assert.Equal((uint)1, v3Agg.CurrentAmount());
            }
        }

        [Fact]
        public void throws_on_requesting_specific_version_higher_than_exists() {
            foreach (var repo in _repos) {
                var id = Guid.NewGuid();
                var tAgg = new TestAggregate(id);
                tAgg.RaiseBy(1);
                tAgg.RaiseBy(1);
                tAgg.RaiseBy(1);
                tAgg.RaiseBy(1); //v4
                repo.Save(tAgg);
                Assert.Throws<AggregateVersionException>(() => repo.GetById<TestAggregate>(id, 50));
            }
        }

        [Fact]
        public void can_get_aggregate_at_version() {
            foreach (var repo in _repos) {
                var id = Guid.NewGuid();
                var tAgg = new TestAggregate(id);
                tAgg.RaiseBy(1);
                Assert.Equal((uint)1, tAgg.CurrentAmount());
                tAgg.RaiseBy(2);
                Assert.Equal((uint)3, tAgg.CurrentAmount());
                // get latest version (v3)
                repo.Save(tAgg);
                var v3Agg = repo.GetById<TestAggregate>(id);
                Assert.Equal((uint)3, v3Agg.CurrentAmount());

                //get version v2
                var v2Agg = repo.GetById<TestAggregate>(id, 2);
                Assert.Equal((uint)1, v2Agg.CurrentAmount());

            }
        }

        [Fact]
        public void will_throw_concurrency_exception() {
            foreach (var repo in _repos) {
                var id = Guid.NewGuid();
                var tAgg = new TestAggregate(id);
                tAgg.RaiseBy(1);
                Assert.Equal((uint)1, tAgg.CurrentAmount());

                tAgg.RaiseBy(2);
                Assert.Equal((uint)3, tAgg.CurrentAmount());

                // get latest version (v3) then update & save
                repo.Save(tAgg);
                var v3Agg = repo.GetById<TestAggregate>(id);
                v3Agg.RaiseBy(2);
                repo.Save(v3Agg);

                //Update & save original copy
                tAgg.RaiseBy(6);
                var r = repo; //copy iteration varible for closure
                Assert.Throws<AggregateException>(() => r.Save(tAgg));
            }
        }

        [Fact]
        public void can_multiple_update_and_save_multiple_aggregates() {
            foreach (var repo in _repos) {
               var id1 = Guid.NewGuid();
                var tAgg = new TestAggregate(id1);
                tAgg.RaiseBy(1);
                tAgg.RaiseBy(2);
                tAgg.RaiseBy(3);
                repo.Save(tAgg);
                var loadedAgg1 = repo.GetById<TestAggregate>(id1);
                Assert.True(tAgg.CurrentAmount() == loadedAgg1.CurrentAmount());

                var id2 = Guid.NewGuid();
                var tAgg2 = new TestAggregate(id2);
                tAgg2.RaiseBy(4);
                tAgg2.RaiseBy(5);
                tAgg2.RaiseBy(6);
                repo.Save(tAgg2);
                var loadedAgg2 = repo.GetById<TestAggregate>(id2);
                Assert.True(tAgg2.CurrentAmount() == loadedAgg2.CurrentAmount());
            }
        }
    }
    // ReSharper restore InconsistentNaming
}
