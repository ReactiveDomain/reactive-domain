using ReactiveDomain.Foundation;
using System;
using System.Collections.Generic;
using System.Threading;
using ReactiveDomain.Messaging;
using ReactiveDomain.Testing.EventStore;
using Xunit;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Testing {
    // todo: separate stream connection tests and repo tests
    // ReSharper disable InconsistentNaming
    public class MockStreamStoreConnectionTests : IClassFixture<StreamStoreConnectionFixture> {
        private readonly List<IStreamStoreConnection> _streamStoreConnections = new List<IStreamStoreConnection>();

        private readonly List<IRepository> _repos = new List<IRepository>();
        private readonly IStreamNameBuilder _streamNameBuilder;
        
        public MockStreamStoreConnectionTests(StreamStoreConnectionFixture fixture) {
         
            _streamNameBuilder = new PrefixedCamelCaseStreamNameBuilder("UnitTest");
            
            var mockStreamStore = new MockStreamStoreConnection("Test");
            _streamStoreConnections.Add(mockStreamStore);
            mockStreamStore.Connect();
            _streamStoreConnections.Add(fixture.Connection);

            _repos.Add(new StreamStoreRepository(_streamNameBuilder, mockStreamStore, new JsonMessageSerializer()));

            _repos.Add(new StreamStoreRepository(_streamNameBuilder, fixture.Connection, new JsonMessageSerializer()));
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
        public void can_try_get_new_aggregate() {
            foreach (var repo in _repos) {
                var id = Guid.NewGuid();
                Assert.False(repo.TryGetById(id, out TestAggregate tAgg));
                Assert.Null(tAgg);
                tAgg = new TestAggregate(id);
                repo.Save(tAgg);
                
                Assert.True(repo.TryGetById(id, out TestAggregate rAgg));
                Assert.NotNull(rAgg);
                Assert.Equal(tAgg.Id, rAgg.Id);
            }
        }
        [Fact]
        public void can_try_get_new_aggregate_at_version() {
           
            foreach (var repo in _repos) {
                var id = Guid.NewGuid();
                Assert.False(repo.TryGetById(id, 1, out TestAggregate tAgg));
                Assert.Null(tAgg);
                tAgg = new TestAggregate(id);
                repo.Save(tAgg);
                
                Assert.True(repo.TryGetById(id, 1, out TestAggregate rAgg));
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
                Assert.Throws<InvalidOperationException>(() => repo.GetById<TestAggregate>(id, 0));
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
                Assert.Throws<WrongExpectedVersionException>(() => r.Save(tAgg));
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
        public class TestEvent : Event {
            public readonly int MessageNumber;
            public TestEvent(
                int messageNumber
                ) : base(NewRoot()) {
                MessageNumber = messageNumber;
            }
        }
    }

    // ReSharper restore InconsistentNaming
}
