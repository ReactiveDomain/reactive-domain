using ReactiveDomain.Foundation;
using ReactiveDomain.Foundation.EventStore;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Messaging.Testing;
using ReactiveDomain.Testing.EventStore;
using System;
using System.Collections.Generic;
using Xunit;

namespace ReactiveDomain.Testing {
    // ReSharper disable InconsistentNaming
    public class MockStreamStoreConnectionTests : IClassFixture<StreamStoreConnectionFixture> {
        private readonly List<IRepository> _repos = new List<IRepository>();
        private readonly IBus _bus;
        private readonly IStreamNameBuilder _streamNameBuilder;

        public MockStreamStoreConnectionTests(StreamStoreConnectionFixture fixture) {
            _streamNameBuilder = new PrefixedCamelCaseStreamNameBuilder("UnitTest");
            _bus = new InMemoryBus("Mock Event Store Post Commit Target");
            //todo: reconnect bus to the all stream subscription
            //todo: re-add MockStreamStoreConnection
            //_repos.Add(new StreamStoreRepository(_streamNameBuilder, new MockStreamStoreConnection("Test")));
            _repos.Add(new StreamStoreRepository(_streamNameBuilder, fixture.Connection));
        }
        //TODO: Add Subscription Tests
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
        public void can_subscribe_to_all() {

            foreach (var repo in _repos) {
                
                var id = Guid.NewGuid();
                var tAgg = new TestAggregate(id);
                tAgg.RaiseBy(1);
                tAgg.RaiseBy(1);
                tAgg.RaiseBy(1);
                repo.Save(tAgg);
                //todo: fix
              //  Assert.Equal(4, q.Messages.Count);
            }
        }

        [Fact]
        public void can_replay_all() {
            foreach (var repo in _repos) {
                //todo: fix
                if (!(repo is IRepository r)) continue;
               // var q = new TestQueue(_bus);

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
                //var q = new TestQueue(_bus);

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
               // var q = new TestQueue(_bus);

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
