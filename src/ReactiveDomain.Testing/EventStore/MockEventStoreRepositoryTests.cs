using ReactiveDomain.Foundation;
using ReactiveDomain.Foundation.EventStore;
using ReactiveDomain.Foundation.Testing;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Messaging.Testing;
using ReactiveDomain.Testing.EventStore;
using System;
using System.Collections.Generic;
using Xunit;

namespace ReactiveDomain.Testing
{
    // ReSharper disable InconsistentNaming
    public class MockEventStoreRepositoryTests : IClassFixture<EmbeddedEventStoreFixture>
    {
        private readonly List<IRepository> _repos = new List<IRepository>();
        private readonly IBus _bus;
        private readonly IStreamNameBuilder _streamNameBuilder;

        public MockEventStoreRepositoryTests(EmbeddedEventStoreFixture fixture)
        {
            _streamNameBuilder = new PrefixedCamelCaseStreamNameBuilder("UnitTest");
            _bus = new InMemoryBus("Mock Event Store Post Commit Target");
            _repos.Add(new MockEventStoreRepository(_streamNameBuilder, _bus));
            _repos.Add(new EventStoreRepository(_streamNameBuilder, fixture.Connection));
        }

        [Fact]
        public void can_save_new_aggregate()
        {
            foreach (var repo in _repos)
            {
                var id = Guid.NewGuid();
                var tAgg = new TestAggregate(id);
                repo.Save(tAgg);
                var rAgg = repo.GetById<TestAggregate>(id);
                Assert.NotNull(rAgg);
                Assert.Equal(tAgg.Id, rAgg.Id);
            }
        }

        [Fact]
        public void can_update_and_save_aggregate()
        {
            foreach (var repo in _repos)
            {
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
        public void throws_on_requesting_specific_version_higher_than_exists()
        {
            foreach (var repo in _repos)
            {
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
        public void can_get_aggregate_at_version()
        {
            foreach (var repo in _repos)
            {
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
        public void will_throw_concurrency_exception()
        {
            foreach (var repo in _repos)
            {
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
        public void can_subscribe_to_all()
        {

            foreach (var repo in _repos)
            {
                var r = repo as MockEventStoreRepository;
                if (r == null) continue;
                var q = new TestQueue(_bus);

                var id = Guid.NewGuid();
                var tAgg = new TestAggregate(id);
                tAgg.RaiseBy(1);
                tAgg.RaiseBy(1);
                tAgg.RaiseBy(1);
                repo.Save(tAgg);

                Assert.Equal(4, q.Messages.Count);
            }
        }

        [Fact]
        public void can_replay_all()
        {
            foreach (var repo in _repos)
            {
                if (!(repo is MockEventStoreRepository r)) continue;
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

                Assert.Equal(8, q.Messages.Count);
                var bus = new InMemoryBus("all");
                var q2 = new TestQueue(bus);
                r.ReplayAllOnto(bus);
                q2.Messages
                     .AssertNext<TestAggregateMessages.NewAggregate>(
                        correlationId: id1)
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 1, "Event mismatch wrong amount")
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 2, "Event mismatch wrong amount")
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 3, "Event mismatch wrong amount")
                    .AssertNext<TestAggregateMessages.NewAggregate>(
                        correlationId: id2)
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 4, "Event mismatch wrong amount")
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 5, "Event mismatch wrong amount")
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 6, "Event mismatch wrong amount")
                    .AssertEmpty();
            }
        }
        [Fact]
        public void can_get_by_stream()
        {
            foreach (var repo in _repos)
            {
                var r = repo as MockEventStoreRepository;
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

                Assert.Equal(8, q.Messages.Count);
                var bus = new InMemoryBus("all");
                var q2 = new TestQueue(bus);
                r.ReplayStreamOnto(bus, _streamNameBuilder.GenerateForAggregate(typeof(TestAggregate), id1));
                q2.Messages
                    .AssertNext<TestAggregateMessages.NewAggregate>(
                        correlationId: id1)
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 1, "Event mismatch wrong amount")
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 2, "Event mismatch wrong amount")
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 3, "Event mismatch wrong amount");

                r.ReplayStreamOnto(bus, _streamNameBuilder.GenerateForAggregate(typeof(TestAggregate), id2));
                q2.Messages
                    .AssertNext<TestAggregateMessages.NewAggregate>(
                        correlationId: id2)
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 4, "Event mismatch wrong amount")
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 5, "Event mismatch wrong amount")
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 6, "Event mismatch wrong amount")
                    .AssertEmpty();
            }
        }
        [Fact]
        public void can_get_by_category()
        {
            foreach (var repo in _repos)
            {
                var r = repo as MockEventStoreRepository;
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

                Assert.Equal(12, q.Messages.Count);
                var bus = new InMemoryBus("all");
                var q2 = new TestQueue(bus);
                r.ReplayCategoryOnto(bus, "testAggregate");
                q2.Messages
                    .AssertNext<TestAggregateMessages.NewAggregate>(
                        correlationId: id1)
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 1, "Event mismatch wrong amount")
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 2, "Event mismatch wrong amount")
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 3, "Event mismatch wrong amount")
                    .AssertNext<TestAggregateMessages.NewAggregate>(
                        correlationId: id2)
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 4, "Event mismatch wrong amount")
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 5, "Event mismatch wrong amount")
                    .AssertNext<TestAggregateMessages.Increment>(
                        m => m.Amount == 6, "Event mismatch wrong amount")
                    .AssertEmpty();
                r.ReplayCategoryOnto(bus, "testWoftamAggregate");
                q2.Messages
                    .AssertNext<TestWoftamAggregateCreated>(
                        correlationId: id3)
                    .AssertNext<WoftamEvent>(
                        correlationId: Guid.Empty)
                    .AssertNext<WoftamEvent>(
                        correlationId: Guid.Empty)
                   .AssertNext<WoftamEvent>(
                        correlationId: Guid.Empty)
                   .AssertEmpty();
            }
        }
    }
    // ReSharper restore InconsistentNaming
}
