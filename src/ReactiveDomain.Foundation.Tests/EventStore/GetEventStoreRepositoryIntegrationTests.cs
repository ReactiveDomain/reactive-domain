using System;
using System.Linq;
using System.Text;
using EventStore.ClientAPI;
using Newtonsoft.Json.Linq;
using ReactiveDomain.Foundation.EventStore;
using Xunit;

namespace ReactiveDomain.Foundation.Tests.EventStore
{
    /// <summary>
    /// Integration tests for the GetEventStoreRepository. 
    /// </summary>
    [Collection("ESEmbeded")]
    public class GetEventStoreRepositoryIntegrationTests 
    {
        private static readonly TimeSpan TimeToStop = TimeSpan.FromSeconds(5);
       
        private static Guid SaveTestAggregateWithoutCustomHeaders(IRepository repository, int numberOfEvents)
        {
            var aggregateToSave = new TestWoftamAggregate(Guid.NewGuid());
            aggregateToSave.ProduceEvents(numberOfEvents);
            repository.Save(aggregateToSave, Guid.NewGuid(), d => { });
            return aggregateToSave.Id;
        }
        
        private readonly GetEventStoreRepository _repo;
        private readonly IEventStoreConnection _connection;

        public GetEventStoreRepositoryIntegrationTests(EmbeddedEventStoreFixture fixture)
        {
            _connection = fixture.Connection;
            _repo = new GetEventStoreRepository(_connection);
        }


        public void CanGetLatestVersionById()
        {
            var savedId = SaveTestAggregateWithoutCustomHeaders(_repo, 3000 /* excludes TestAggregateCreated */);

            var retrieved = _repo.GetById<TestWoftamAggregate>(savedId);
            
            Assert.Equal(3000, retrieved.AppliedEventCount);
        }

        public void CanGetSpecificVersionFromFirstPageById()
        {
            var savedId = SaveTestAggregateWithoutCustomHeaders(_repo, 100 /* excludes TestAggregateCreated */);

            var retrieved = _repo.GetById<TestWoftamAggregate>(savedId, 65);
            Assert.Equal(64, retrieved.AppliedEventCount);
        }

        public void CanGetSpecificVersionFromSubsequentPageById()
        {
            var savedId = SaveTestAggregateWithoutCustomHeaders(_repo, 500 /* excludes TestAggregateCreated */);

            var retrieved = _repo.GetById<TestWoftamAggregate>(savedId, 126);
            Assert.Equal(125, retrieved.AppliedEventCount);
        }
        //TODO fix this
        //It looks like the eventstore is choking on writing
        //the catageory and event type streams for this
        // It just keeps logging checkpoints for them at very low numbers
        [Fact(Skip = "Eventstore bug???")]
        public void CanHandleLargeNumberOfEventsInOneTransaction()
        {
            const int numberOfEvents = 50000;

            var aggregateId = SaveTestAggregateWithoutCustomHeaders(_repo, numberOfEvents /* excludes TestAggregateCreated */);

            var saved = _repo.GetById<TestWoftamAggregate>(aggregateId);
            Assert.Equal(numberOfEvents, saved.AppliedEventCount);
        }

        public void CanSaveExistingAggregate()
        {
            var savedId = SaveTestAggregateWithoutCustomHeaders(_repo, 100 /* excludes TestAggregateCreated */);

            var firstSaved = _repo.GetById<TestWoftamAggregate>(savedId);
            firstSaved.ProduceEvents(50);
            _repo.Save(firstSaved, Guid.NewGuid(), d => { });

            var secondSaved = _repo.GetById<TestWoftamAggregate>(savedId);
            Assert.Equal(150, secondSaved.AppliedEventCount);
        }

        public void CanSaveMultiplesOfWritePageSize()
        {
            var savedId = SaveTestAggregateWithoutCustomHeaders(_repo, 1500 /* excludes TestAggregateCreated */);
            var saved = _repo.GetById<TestWoftamAggregate>(savedId);

            Assert.Equal(1500, saved.AppliedEventCount);
        }

        public void ClearsEventsFromAggregateOnceCommitted()
        {
            var aggregateToSave = new TestWoftamAggregate(Guid.NewGuid());
            aggregateToSave.ProduceEvents(10);
            _repo.Save(aggregateToSave, Guid.NewGuid(), d => { });

            Assert.Equal(0, ((IEventSource)aggregateToSave).TakeEvents().Count());
        }

        public void ThrowsOnRequestingSpecificVersionHigherThanExists()
        {
            var aggregateId = SaveTestAggregateWithoutCustomHeaders(_repo, 10);

            Assert.Throws<AggregateVersionException>(() => _repo.GetById<TestWoftamAggregate>(aggregateId, 50));
        }

        public void GetsEventsFromCorrectStreams()
        {
            var aggregate1Id = SaveTestAggregateWithoutCustomHeaders(_repo, 100);
            var aggregate2Id = SaveTestAggregateWithoutCustomHeaders(_repo, 50);

            var firstSaved = _repo.GetById<TestWoftamAggregate>(aggregate1Id);
            Assert.Equal(100, firstSaved.AppliedEventCount);

            var secondSaved = _repo.GetById<TestWoftamAggregate>(aggregate2Id);
            Assert.Equal(50, secondSaved.AppliedEventCount);
        }

        public void ThrowsOnGetNonExistentAggregate()
        {
            Assert.Throws<AggregateNotFoundException>(() => _repo.GetById<TestWoftamAggregate>(Guid.NewGuid()));
        }

        public void ThrowsOnGetDeletedAggregate()
        {
            var aggregateId = SaveTestAggregateWithoutCustomHeaders(_repo, 10);

            var streamName = $"testWoftamAggregate-{aggregateId.ToString("N")}";
            _connection.DeleteStreamAsync(streamName, 10).Wait();

            // Assert.Throws<AggregateDeletedException>(() => _repo.GetById<TestAggregate>(aggregateId));
            //Looks like an api change
            Assert.Throws<AggregateNotFoundException>(() => _repo.GetById<TestWoftamAggregate>(aggregateId));
        }

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
    }
}