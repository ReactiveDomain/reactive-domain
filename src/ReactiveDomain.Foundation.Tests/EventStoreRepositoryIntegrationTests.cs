using System;
using System.Linq;
using System.Text;
using EventStore.ClientAPI;
using Newtonsoft.Json.Linq;
using ReactiveDomain.Foundation.EventStore;
using ReactiveDomain.Foundation.Testing;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Foundation.Tests
{
    /// <summary>
    /// Integration tests for the GetEventStoreRepository. 
    /// </summary>
    [Collection(nameof(EventStoreCollection))]
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

        private readonly EventStoreRepository _repo;
        private readonly IEventStoreConnection _connection;
        private readonly StreamNameBuilder _streamNameBuilder;

        public EventStoreRepositoryIntegrationTests(EmbeddedEventStoreFixture fixture)
        {
            _connection = fixture.Connection;
            _streamNameBuilder = new StreamNameBuilder(DomainPrefix);
            _repo = new EventStoreRepository(_streamNameBuilder, _connection);
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
            var streamName = _streamNameBuilder.Generate(typeof(TestWoftamAggregate), aggregateId);
            _connection.DeleteStreamAsync(streamName, 10).Wait();

            // Assert.Throws<AggregateDeletedException>(() => _repo.GetById<TestAggregate>(aggregateId));
            //Looks like an api change
            Assert.Throws<AggregateNotFoundException>(() => _repo.GetById<TestWoftamAggregate>(aggregateId));
        }

        [Fact]
        public void SavesCommitHeadersOnEachEvent()
        {
            var aggregateToSave = new TestWoftamAggregate(Guid.NewGuid());
            aggregateToSave.ProduceEvents(20);
            _repo.Save(aggregateToSave, d =>
            {
                d.Add("CustomHeader1", "CustomValue1");
                d.Add("CustomHeader2", "CustomValue2");
            });

            var read = _connection.ReadStreamEventsForwardAsync($"aggregate-{aggregateToSave.Id}", 1, 20, false).Result;
            foreach (var serializedEvent in read.Events)
            {
                var parsedMetadata = JObject.Parse(Encoding.UTF8.GetString(serializedEvent.OriginalEvent.Metadata));

                var deserializedCommitId = parsedMetadata.Property("CommitId").Value.ToObject<Guid>();
                Assert.NotNull(deserializedCommitId);

                var deserializedCustomHeader1 = parsedMetadata.Property("CustomHeader1").Value.ToObject<string>();
                Assert.Equal("CustomValue1", deserializedCustomHeader1);

                var deserializedCustomHeader2 = parsedMetadata.Property("CustomHeader2").Value.ToObject<string>();
                Assert.Equal("CustomValue2", deserializedCustomHeader2);
            }
        }
    }
}