using System;
using System.IO;
using System.Net;
using System.Text;
using EventStore.ClientAPI;
using Newtonsoft.Json.Linq;
using ReactiveDomain.Domain;
using ReactiveDomain.EventStore;
using Xunit;

namespace ReactiveDomain.Tests.EventStore
{
    /// <summary>
    /// Integration tests for the GetEventStoreRepository. These tests require a
    /// running version of the Event Store, with a TCP endpoint as specified in the
    /// IntegrationTestTcpEndPoint field (defaults to local loopback, port 1113).
    /// </summary>

    public class GetEventStoreRepositoryIntegrationTests : IDisposable
    {
       
        /// <summary>
        /// Set this to the TCP endpoint on which the Event Store is running.
        /// </summary>
        private static readonly IPEndPoint IntegrationTestTcpEndPoint = new IPEndPoint(IPAddress.Loopback, 1113);

        private static Guid SaveTestAggregateWithoutCustomHeaders(IRepository repository, int numberOfEvents)
        {
            var aggregateToSave = new TestWoftamAggregate(Guid.NewGuid());
            aggregateToSave.ProduceEvents(numberOfEvents);
            repository.Save(aggregateToSave, Guid.NewGuid(), d => { });
            return aggregateToSave.Id;
        }

        private readonly IEventStoreConnection _connection;
        private readonly ReactiveDomain.EventStore.GetEventStoreRepository _repo;


        public GetEventStoreRepositoryIntegrationTests()
        {
            var es = new EventStoreLoader();
            es.SetupEventStore(new DirectoryInfo(@"C:\Program Files\PerkinElmer\Greylock\EventStore"));
            _connection = EventStoreConnection.Create(IntegrationTestTcpEndPoint);
            _connection.ConnectAsync().Wait();
            _repo = new ReactiveDomain.EventStore.GetEventStoreRepository(_connection);
        }


        [Fact(Skip = "EventStore")]
        public void CanGetLatestVersionById()
        {
            var savedId = SaveTestAggregateWithoutCustomHeaders(_repo, 3000 /* excludes TestAggregateCreated */);

            var retrieved = _repo.GetById<TestWoftamAggregate>(savedId);
            Assert.Equal(3001, retrieved.Version);
            Assert.Equal(3000, retrieved.AppliedEventCount);
        }

        [Fact(Skip = "EventStore")]
        public void CanGetSpecificVersionFromFirstPageById()
        {
            var savedId = SaveTestAggregateWithoutCustomHeaders(_repo, 100 /* excludes TestAggregateCreated */);

            var retrieved = _repo.GetById<TestWoftamAggregate>(savedId, 65);
            Assert.Equal(65, retrieved.Version);
            Assert.Equal(64, retrieved.AppliedEventCount);
        }

        [Fact(Skip = "EventStore")]
        public void CanGetSpecificVersionFromSubsequentPageById()
        {
            var savedId = SaveTestAggregateWithoutCustomHeaders(_repo, 500 /* excludes TestAggregateCreated */);

            var retrieved = _repo.GetById<TestWoftamAggregate>(savedId, 126);
            Assert.Equal(126, retrieved.Version);
            Assert.Equal(125, retrieved.AppliedEventCount);
        }

        [Fact(Skip = "EventStore")]
        public void CanHandleLargeNumberOfEventsInOneTransaction()
        {
            const int numberOfEvents = 50000;

            var aggregateId = SaveTestAggregateWithoutCustomHeaders(_repo, numberOfEvents /* excludes TestAggregateCreated */);

            var saved = _repo.GetById<TestWoftamAggregate>(aggregateId);
            Assert.Equal(numberOfEvents + 1, saved.Version);
            Assert.Equal(numberOfEvents, saved.AppliedEventCount);
        }

        [Fact(Skip = "EventStore")]
        public void CanSaveExistingAggregate()
        {
            var savedId = SaveTestAggregateWithoutCustomHeaders(_repo, 100 /* excludes TestAggregateCreated */);

            var firstSaved = _repo.GetById<TestWoftamAggregate>(savedId);
            firstSaved.ProduceEvents(50);
            _repo.Save(firstSaved, Guid.NewGuid(), d => { });

            var secondSaved = _repo.GetById<TestWoftamAggregate>(savedId);
            Assert.Equal(150, secondSaved.AppliedEventCount);
        }

        [Fact(Skip = "EventStore")]
        public void CanSaveMultiplesOfWritePageSize()
        {
            var savedId = SaveTestAggregateWithoutCustomHeaders(_repo, 1500 /* excludes TestAggregateCreated */);
            var saved = _repo.GetById<TestWoftamAggregate>(savedId);

            Assert.Equal(1500, saved.AppliedEventCount);
        }

        [Fact(Skip = "EventStore")]
        public void ClearsEventsFromAggregateOnceCommitted()
        {
            var aggregateToSave = new TestWoftamAggregate(Guid.NewGuid());
            aggregateToSave.ProduceEvents(10);
            _repo.Save(aggregateToSave, Guid.NewGuid(), d => { });

            Assert.Equal(0, ((IAggregate)aggregateToSave).GetUncommittedEvents().Count);
        }

        [Fact(Skip = "EventStore")]
        public void ThrowsOnRequestingSpecificVersionHigherThanExists()
        {
            var aggregateId = SaveTestAggregateWithoutCustomHeaders(_repo, 10);

            Assert.Throws<AggregateVersionException>(() => _repo.GetById<TestWoftamAggregate>(aggregateId, 50));
        }

        [Fact(Skip = "EventStore")]
        public void GetsEventsFromCorrectStreams()
        {
            var aggregate1Id = SaveTestAggregateWithoutCustomHeaders(_repo, 100);
            var aggregate2Id = SaveTestAggregateWithoutCustomHeaders(_repo, 50);

            var firstSaved = _repo.GetById<TestWoftamAggregate>(aggregate1Id);
            Assert.Equal(100, firstSaved.AppliedEventCount);

            var secondSaved = _repo.GetById<TestWoftamAggregate>(aggregate2Id);
            Assert.Equal(50, secondSaved.AppliedEventCount);
        }

        [Fact(Skip = "EventStore")]
        public void ThrowsOnGetNonExistentAggregate()
        {
            Assert.Throws<AggregateNotFoundException>(() => _repo.GetById<TestWoftamAggregate>(Guid.NewGuid()));
        }

        [Fact(Skip = "EventStore")]
        public void ThrowsOnGetDeletedAggregate()
        {
            var aggregateId = SaveTestAggregateWithoutCustomHeaders(_repo, 10);

            var streamName = $"testWoftamAggregate-{aggregateId.ToString("N")}";
            _connection.DeleteStreamAsync(streamName, 10).Wait();

            // Assert.Throws<AggregateDeletedException>(() => _repo.GetById<TestAggregate>(aggregateId));
            //Looks like an api change
            Assert.Throws<AggregateNotFoundException>(() => _repo.GetById<TestWoftamAggregate>(aggregateId));
        }

        [Fact(Skip = "EventStore")]
        public void SavesCommitHeadersOnEachEvent()
        {
            var commitId = Guid.NewGuid();
            var aggregateToSave = new TestWoftamAggregate(Guid.NewGuid());
            aggregateToSave.ProduceEvents(20);
            _repo.Save(aggregateToSave, commitId, d =>
            {
                d.Add("CustomHeader1", "CustomValue1");
                d.Add("CustomHeader2", "CustomValue2");
            });

            var read = _connection.ReadStreamEventsForwardAsync($"aggregate-{aggregateToSave.Id}", 1, 20, false).Result;
            foreach (var serializedEvent in read.Events)
            {
                var parsedMetadata = JObject.Parse(Encoding.UTF8.GetString(serializedEvent.OriginalEvent.Metadata));
                var deserializedCommitId = parsedMetadata.Property("CommitId").Value.ToObject<Guid>();
                Assert.Equal(commitId, deserializedCommitId);

                var deserializedCustomHeader1 = parsedMetadata.Property("CustomHeader1").Value.ToObject<string>();
                Assert.Equal("CustomValue1", deserializedCustomHeader1);

                var deserializedCustomHeader2 = parsedMetadata.Property("CustomHeader2").Value.ToObject<string>();
                Assert.Equal("CustomValue2", deserializedCustomHeader2);
            }
        }

        public void Dispose()
        {
            _connection.Close();
        }
    }
}