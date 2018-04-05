using ReactiveDomain.Foundation;
using ReactiveDomain.Foundation.EventStore;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Messaging.Testing;
using ReactiveDomain.Util;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ReactiveDomain.Testing
{
    // todo: separate stream connection tests and repo tests
    // ReSharper disable InconsistentNaming
    public class MockStreamStoreConnectionTests : IClassFixture<StreamStoreConnectionFixture> {
        private readonly List<IStreamStoreConnection> _streamStoreConnections = new List<IStreamStoreConnection>();

        private readonly List<IRepository> _repos = new List<IRepository>();
        private readonly IBus _bus;
        private readonly IStreamNameBuilder _streamNameBuilder;
        private readonly UserCredentials _admin;

        public MockStreamStoreConnectionTests(StreamStoreConnectionFixture fixture)
        {
            _admin = fixture.AdminCredentials;
            _streamNameBuilder = new PrefixedCamelCaseStreamNameBuilder("UnitTest");
            _bus = new InMemoryBus("Mock Event Store Post Commit Target");

            // _streamStoreConnections.Add(new MockStreamStoreConnection("Test"));
            _streamStoreConnections.Add(fixture.Connection);

            //todo: reconnect bus to the all stream subscription
            // _repos.Add(new StreamStoreRepository(_streamNameBuilder, mockConnection));
            _repos.Add(new StreamStoreRepository(_streamNameBuilder, fixture.Connection));
        }

        [Fact]
        public void can_delete_stream()
        {
            var streamName = _streamNameBuilder.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());
            foreach (var conn in _streamStoreConnections)
            {
                var createEvent = new EventData(Guid.NewGuid(), typeof(TestAggregateMessages.NewAggregate).Name, true, new byte[] { 0 }, new byte[] { 0 });
                conn.AppendToStream(streamName, ExpectedVersion.Any, null, createEvent);

                conn.DeleteStream(streamName, ExpectedVersion.Any);

                // We do not support soft delete so StreamNotFoundSlice instead of StreamDeletedSlice
                Assert.IsOrBecomesTrue(() => conn.ReadStreamForward(streamName, 0, 1) is StreamNotFoundSlice, msg: "Failed to delete stream");
            }
        }

        [Fact]
        public void can_read_stream_forward()
        {
            var streamName = _streamNameBuilder.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());
            var numEventsToBeSent = 5;
            long count = 2;
            long startFrom = numEventsToBeSent - count; // We want to get the <count> last events (forward)

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

                Assert.IsNotType<StreamNotFoundSlice>(slice);
                Assert.IsNotType<StreamDeletedSlice>(slice);
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
            long startFrom = count - 1; // We want to get the <count> first events (backward), and -1 because 0 based

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

                Assert.IsNotType<StreamNotFoundSlice>(slice);
                Assert.IsNotType<StreamDeletedSlice>(slice);
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

            foreach (var conn in _streamStoreConnections)
            {
                var capturedEvents = new List<RecordedEvent>();
                var dropped = false;

                var sub = conn.SubscribeToStream(
                    streamName,
                    async evt => { capturedEvents.Add(evt); await Task.FromResult(Unit.Default); },
                    (reason, ex) => dropped = true
                );

                var eventData = new EventData(Guid.NewGuid(), typeof(TestAggregateMessages.NewAggregate).Name, true, new byte[] { 0 }, new byte[] { 0 });
                conn.AppendToStream(streamName, ExpectedVersion.Any, null, eventData);
                Assert.IsOrBecomesTrue(() => 1 == capturedEvents.Count, msg: "Failed to capture events");
                Assert.Equal(eventData.EventId, capturedEvents[0].EventId);
                Assert.Equal(eventData.Type, capturedEvents[0].EventType);
                Assert.Equal(eventData.Data, capturedEvents[0].Data);
                Assert.Equal(eventData.Metadata, capturedEvents[0].Metadata);

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
            var lastCheckpoint = numEventsToBeSent - count - 1;  // we want to capture the <count> last events and -1 since it is 0 based
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
                    (reason, ex) => dropped = true
                );

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
            foreach (var conn in _streamStoreConnections)
            {
                var capturedEvents = new List<RecordedEvent>();
                var dropped = false;

                var sub = conn.SubscribeToAll(
                    async evt => { capturedEvents.Add(evt); await Task.FromResult(Unit.Default); },
                    (reason, ex) => dropped = true,
                    _admin
                );

                // Stream1
                var eventData1 = new EventData(Guid.NewGuid(), typeof(TestAggregateMessages.NewAggregate).Name, true, new byte[] { 0 }, new byte[] { 0 });
                var stream1 = _streamNameBuilder.GenerateForAggregate(typeof(TestAggregate), Guid.NewGuid());
                conn.AppendToStream(stream1, ExpectedVersion.Any, null, eventData1);
                Assert.IsOrBecomesTrue(() => 1 == capturedEvents.Count, msg: $"Failed to capture events on stream {stream1} ");
                Assert.Equal(eventData1.EventId, capturedEvents[0].EventId);
                Assert.Equal(eventData1.Type, capturedEvents[0].EventType);
                Assert.Equal(eventData1.Data, capturedEvents[0].Data);
                Assert.Equal(eventData1.Metadata, capturedEvents[0].Metadata);
                capturedEvents.Clear();

                // Stream2
                var eventData2 = new EventData(Guid.NewGuid(), typeof(TestAggregateMessages.NewAggregate).Name, true, new byte[] { 1 }, new byte[] { 1 });
                var stream2 = _streamNameBuilder.GenerateForAggregate(typeof(TestWoftamAggregate), Guid.NewGuid());
                conn.AppendToStream(stream2, ExpectedVersion.Any, null, eventData2);
                Assert.IsOrBecomesTrue(() => 1 == capturedEvents.Count, msg: $"Failed to capture events on stream {stream2}");
                Assert.Equal(eventData2.EventId, capturedEvents[0].EventId);
                Assert.Equal(eventData2.Type, capturedEvents[0].EventType);
                Assert.Equal(eventData2.Data, capturedEvents[0].Data);
                Assert.Equal(eventData2.Metadata, capturedEvents[0].Metadata);

                sub.Dispose();
                Assert.IsOrBecomesTrue(() => dropped, msg: "Failed to handle drop");
            }
        }

        // todo: review everything from here
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
        public void can_replay_all() {
            foreach (var repo in _repos) {
                //todo: fix
                if (!(repo is IRepository r)) continue;
                var q = new TestQueue(_bus);

                var id1 = Guid.NewGuid();
                var tAgg = new TestAggregate(id1);
                tAgg.RaiseBy(1);
                tAgg.RaiseBy(2);
                tAgg.RaiseBy(3);
                repo.Save(tAgg);

                var id2 = Guid.NewGuid();
                var tAgg2 = new TestAggregate(id2);
                tAgg2.RaiseBy(4);
                tAgg2.RaiseBy(5);
                tAgg2.RaiseBy(6);
                repo.Save(tAgg2);
                //todo: fix
                //Assert.Equal(8, q.Messages.Count);
                //var bus = new InMemoryBus("all");
                //var q2 = new TestQueue(bus);
                //r.ReplayAllOnto(bus);
                //todo: fix
                //q2.Messages
                //     .AssertNext<TestAggregateMessages.NewAggregate>(
                //        correlationId: id1)
                //    .AssertNext<TestAggregateMessages.Increment>(
                //        m => m.Amount == 1, "Event mismatch wrong amount")
                //    .AssertNext<TestAggregateMessages.Increment>(
                //        m => m.Amount == 2, "Event mismatch wrong amount")
                //    .AssertNext<TestAggregateMessages.Increment>(
                //        m => m.Amount == 3, "Event mismatch wrong amount")
                //    .AssertNext<TestAggregateMessages.NewAggregate>(
                //        correlationId: id2)
                //    .AssertNext<TestAggregateMessages.Increment>(
                //        m => m.Amount == 4, "Event mismatch wrong amount")
                //    .AssertNext<TestAggregateMessages.Increment>(
                //        m => m.Amount == 5, "Event mismatch wrong amount")
                //    .AssertNext<TestAggregateMessages.Increment>(
                //        m => m.Amount == 6, "Event mismatch wrong amount")
                //    .AssertEmpty();
            }
        }
        [Fact]
        public void can_get_by_stream() {
            foreach (var repo in _repos) {
                var r = repo as IRepository;
                if (r == null) continue;
                var q = new TestQueue(_bus);

                var id1 = Guid.NewGuid();
                var tAgg = new TestAggregate(id1);
                tAgg.RaiseBy(1);
                tAgg.RaiseBy(2);
                tAgg.RaiseBy(3);
                repo.Save(tAgg);

                var id2 = Guid.NewGuid();
                var tAgg2 = new TestAggregate(id2);
                tAgg2.RaiseBy(4);
                tAgg2.RaiseBy(5);
                tAgg2.RaiseBy(6);
                repo.Save(tAgg2);
                //todo: fix
                //Assert.Equal(8, q.Messages.Count);
                //var bus = new InMemoryBus("all");
                //var q2 = new TestQueue(bus);
                //todo: fix
                //r.ReplayStreamOnto(bus, _streamNameBuilder.GenerateForAggregate(typeof(TestAggregate), id1));
                //q2.Messages
                //    .AssertNext<TestAggregateMessages.NewAggregate>(
                //        correlationId: id1)
                //    .AssertNext<TestAggregateMessages.Increment>(
                //        m => m.Amount == 1, "Event mismatch wrong amount")
                //    .AssertNext<TestAggregateMessages.Increment>(
                //        m => m.Amount == 2, "Event mismatch wrong amount")
                //    .AssertNext<TestAggregateMessages.Increment>(
                //        m => m.Amount == 3, "Event mismatch wrong amount");
                
                //todo: fix
                //r.ReplayStreamOnto(bus, _streamNameBuilder.GenerateForAggregate(typeof(TestAggregate), id2));
                //q2.Messages
                //    .AssertNext<TestAggregateMessages.NewAggregate>(
                //        correlationId: id2)
                //    .AssertNext<TestAggregateMessages.Increment>(
                //        m => m.Amount == 4, "Event mismatch wrong amount")
                //    .AssertNext<TestAggregateMessages.Increment>(
                //        m => m.Amount == 5, "Event mismatch wrong amount")
                //    .AssertNext<TestAggregateMessages.Increment>(
                //        m => m.Amount == 6, "Event mismatch wrong amount")
                //    .AssertEmpty();
            }
        }
        [Fact]
        public void can_get_by_category() {
            foreach (var repo in _repos) {
                //todo: fix
                var r = repo as IRepository;
                if (r == null) continue;
                var q = new TestQueue(_bus);

                var id1 = Guid.NewGuid();
                var tAgg = new TestAggregate(id1);
                tAgg.RaiseBy(1);
                tAgg.RaiseBy(2);
                tAgg.RaiseBy(3);
                repo.Save(tAgg);

                var id3 = Guid.NewGuid();
                var tAgg3 = new TestWoftamAggregate(id3);
                tAgg3.ProduceEvents(3);
                repo.Save(tAgg3);

                var id2 = Guid.NewGuid();
                var tAgg2 = new TestAggregate(id2);
                tAgg2.RaiseBy(4);
                tAgg2.RaiseBy(5);
                tAgg2.RaiseBy(6);
                repo.Save(tAgg2);
                //todo:fix
                //Assert.Equal(12, q.Messages.Count);
                //var bus = new InMemoryBus("all");
                //var q2 = new TestQueue(bus);
                
                //todo: fix
                //r.ReplayCategoryOnto(bus, "testAggregate");
                //q2.Messages
                //    .AssertNext<TestAggregateMessages.NewAggregate>(
                //        correlationId: id1)
                //    .AssertNext<TestAggregateMessages.Increment>(
                //        m => m.Amount == 1, "Event mismatch wrong amount")
                //    .AssertNext<TestAggregateMessages.Increment>(
                //        m => m.Amount == 2, "Event mismatch wrong amount")
                //    .AssertNext<TestAggregateMessages.Increment>(
                //        m => m.Amount == 3, "Event mismatch wrong amount")
                //    .AssertNext<TestAggregateMessages.NewAggregate>(
                //        correlationId: id2)
                //    .AssertNext<TestAggregateMessages.Increment>(
                //        m => m.Amount == 4, "Event mismatch wrong amount")
                //    .AssertNext<TestAggregateMessages.Increment>(
                //        m => m.Amount == 5, "Event mismatch wrong amount")
                //    .AssertNext<TestAggregateMessages.Increment>(
                //        m => m.Amount == 6, "Event mismatch wrong amount")
                //    .AssertEmpty();

                //todo: fix
                //r.ReplayCategoryOnto(bus, "testWoftamAggregate");
                //q2.Messages
                //    .AssertNext<TestWoftamAggregateCreated>(
                //        correlationId: id3)
                //    .AssertNext<WoftamEvent>(
                //        correlationId: Guid.Empty)
                //    .AssertNext<WoftamEvent>(
                //        correlationId: Guid.Empty)
                //   .AssertNext<WoftamEvent>(
                //        correlationId: Guid.Empty)
                //   .AssertEmpty();
            }
            
        }

        [Fact]
        public void SubscriptionsReturnSavedEvents() {
            foreach (var repo in _repos) {
                var id1 = Guid.NewGuid();
                var tAgg = new TestAggregate(id1);
                tAgg.RaiseBy(1);
                tAgg.RaiseBy(2);
                tAgg.RaiseBy(3);
                repo.Save(tAgg);
               //todo: implement the rest of subscriptions
            }
        }
    }
    // ReSharper restore InconsistentNaming
}
