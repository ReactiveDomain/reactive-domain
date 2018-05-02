using System;
using System.Linq;
using ReactiveDomain.Foundation;
using ReactiveDomain.Foundation.EventStore;
using Xunit;

namespace ReactiveDomain.Testing
{
    /// <summary>
    /// Integration tests for the GetEventStoreRepository. 
    /// </summary>
    [Collection(nameof(EmbeddedStreamStoreConnectionCollection))]
    public class EventStoreRepositoryIntegrationTests
    {
        private const string DomainPrefix = "UnitTest";
        private static readonly TimeSpan TimeToStop = TimeSpan.FromSeconds(5);


        private static Guid SaveTestAggregateWithoutCustomHeaders(IRepository repository, int numberOfEvents)
        {
            var aggregateToSave = new TestWoftamAggregate(Guid.NewGuid());
            aggregateToSave.ProduceEvents(numberOfEvents);
            repository.Save(aggregateToSave);
            return aggregateToSave.Id;
        }

        private readonly StreamStoreRepository _repo;
        private readonly IStreamStoreConnection _connection;
        private readonly IStreamNameBuilder _streamNameBuilder;

        public EventStoreRepositoryIntegrationTests(StreamStoreConnectionFixture fixture)
        {
            _connection = fixture.Connection;
            _streamNameBuilder = new PrefixedCamelCaseStreamNameBuilder(DomainPrefix);
            _repo = new StreamStoreRepository(_streamNameBuilder, _connection, new JsonMessageSerializer());
        }

        [Fact]
        public void CanGetLatestVersionById()
        {
            var savedId = SaveTestAggregateWithoutCustomHeaders(_repo, 3000 /* excludes TestAggregateCreated */);

            var retrieved = _repo.GetById<TestWoftamAggregate>(savedId);

            Assert.Equal(3000, retrieved.AppliedEventCount);
        }

        [Fact]
        public void CanGetSpecificVersionFromFirstPageById()
        {
            var savedId = SaveTestAggregateWithoutCustomHeaders(_repo, 100 /* excludes TestAggregateCreated */);

            var retrieved = _repo.GetById<TestWoftamAggregate>(savedId, 65);
            Assert.Equal(64, retrieved.AppliedEventCount);
        }

        [Fact]
        public void CanGetSpecificVersionFromSubsequentPageById()
        {
            var savedId = SaveTestAggregateWithoutCustomHeaders(_repo, 500 /* excludes TestAggregateCreated */);

            var retrieved = _repo.GetById<TestWoftamAggregate>(savedId, 126);
            Assert.Equal(125, retrieved.AppliedEventCount);
        }
        //TODO fix this
        //It looks like the eventstore is choking on writing
        //the category and event type streams for this
        // It just keeps logging checkpoints for them at very low numbers
        [Fact(Skip = "Eventstore bug???")]
        public void CanHandleLargeNumberOfEventsInOneTransaction()
        {
            const int numberOfEvents = 50000;

            var aggregateId = SaveTestAggregateWithoutCustomHeaders(_repo, numberOfEvents /* excludes TestAggregateCreated */);

            var saved = _repo.GetById<TestWoftamAggregate>(aggregateId);
            Assert.Equal(numberOfEvents, saved.AppliedEventCount);
        }

        [Fact]
        public void CanSaveExistingAggregate()
        {
            var savedId = SaveTestAggregateWithoutCustomHeaders(_repo, 100 /* excludes TestAggregateCreated */);

            var firstSaved = _repo.GetById<TestWoftamAggregate>(savedId);
            firstSaved.ProduceEvents(50);
            _repo.Save(firstSaved);

            var secondSaved = _repo.GetById<TestWoftamAggregate>(savedId);
            Assert.Equal(150, secondSaved.AppliedEventCount);
        }

        [Fact]
        public void CanSaveMultiplesOfWritePageSize()
        {
            var savedId = SaveTestAggregateWithoutCustomHeaders(_repo, 1500 /* excludes TestAggregateCreated */);
            var saved = _repo.GetById<TestWoftamAggregate>(savedId);

            Assert.Equal(1500, saved.AppliedEventCount);
        }

        [Fact]
        public void ClearsEventsFromAggregateOnceCommitted()
        {
            var aggregateToSave = new TestWoftamAggregate(Guid.NewGuid());
            aggregateToSave.ProduceEvents(10);
            _repo.Save(aggregateToSave);

            Assert.Equal(0, ((IEventSource)aggregateToSave).TakeEvents().Count());
        }

        [Fact]
        public void ThrowsOnRequestingSpecificVersionHigherThanExists()
        {
            var aggregateId = SaveTestAggregateWithoutCustomHeaders(_repo, 10);

            Assert.Throws<AggregateVersionException>(() => _repo.GetById<TestWoftamAggregate>(aggregateId, 50));
        }

        [Fact]
        public void GetsEventsFromCorrectStreams()
        {
            var aggregate1Id = SaveTestAggregateWithoutCustomHeaders(_repo, 100);
            var aggregate2Id = SaveTestAggregateWithoutCustomHeaders(_repo, 50);

            var firstSaved = _repo.GetById<TestWoftamAggregate>(aggregate1Id);
            Assert.Equal(100, firstSaved.AppliedEventCount);

            var secondSaved = _repo.GetById<TestWoftamAggregate>(aggregate2Id);
            Assert.Equal(50, secondSaved.AppliedEventCount);
        }

        [Fact]
        public void ThrowsOnGetNonExistentAggregate()
        {
            Assert.Throws<AggregateNotFoundException>(() => _repo.GetById<TestWoftamAggregate>(Guid.NewGuid()));
        }

        [Fact]
        public void ThrowsOnGetDeletedAggregate()
        {
            var aggregateId = SaveTestAggregateWithoutCustomHeaders(_repo, 10);
            var streamName = _streamNameBuilder.GenerateForAggregate(typeof(TestWoftamAggregate), aggregateId);
            _connection.DeleteStream(new StreamName(streamName), 10);

            // Assert.Throws<AggregateDeletedException>(() => _repo.GetById<TestAggregate>(aggregateId));
            //Looks like an api change
            Assert.Throws<AggregateNotFoundException>(() => _repo.GetById<TestWoftamAggregate>(aggregateId));
        }
    }
}