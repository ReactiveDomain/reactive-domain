using System;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing.EventStore;
using Xunit;

namespace ReactiveDomain.Testing
{
    public sealed class MockRepositoryTests
        : IDisposable,
          IHandleCommand<TestCommands.Command1>
    {
        private readonly MockRepositorySpecification _fixture;

        public MockRepositoryTests()
        {
            _fixture = new MockRepositorySpecification();
            _fixture.Dispatcher.Subscribe<TestCommands.Command1>(this);
        }

        public void Dispose()
        {
            _fixture.Dispatcher.Unsubscribe<TestCommands.Command1>(this);
            _fixture.Dispose();
        }
        [Fact]
        public void can_get_repository_events()
        {
            var id = Guid.NewGuid();
            var aggregate = new TestAggregate(id);
            _fixture.Repository.Save(aggregate);
            _fixture.RepositoryEvents.WaitFor<TestAggregateMessages.NewAggregate>(TimeSpan.FromMilliseconds(500));

            _fixture
                .RepositoryEvents
                .AssertNext<TestAggregateMessages.NewAggregate>(e => e.AggregateId == id, "Aggregate Id Mismatch")
                .AssertEmpty();
        }

        [Fact]
        public void can_clear_queues()
        {
            var evt = new TestEvent();
            _fixture.Dispatcher.Publish(evt);

            var id = Guid.NewGuid();
            var aggregate = new TestAggregate(id);
            _fixture.Repository.Save(aggregate);
            _fixture.RepositoryEvents.WaitFor<TestAggregateMessages.NewAggregate>(TimeSpan.FromMilliseconds(100));

            _fixture
                .TestQueue
                .Messages
                .TryPeek(out var msg);
            Assert.IsType<TestEvent>(msg);
            Assert.Equal(evt.MsgId, msg.MsgId);

            _fixture
                .RepositoryEvents
                .Messages
                .TryPeek(out var repoEvt);
            Assert.IsType<TestAggregateMessages.NewAggregate>(repoEvt);
            Assert.Equal(id, ((TestAggregateMessages.NewAggregate)repoEvt).AggregateId);

            _fixture.ClearQueues();

            _fixture
                .TestQueue
                .AssertEmpty();

            _fixture
                .RepositoryEvents
                .AssertEmpty();
        }

        [Fact]
        public void create_mock_repository_without_a_prefix()
        {
            var id = Guid.NewGuid();
            var expectedStreamName = $"testAggregate-{id:n}";
            var mockRepositoryWithoutPrefix = new MockRepositorySpecification(dataStore: new MockStreamStoreConnection(""));
            var categoryStreamName = mockRepositoryWithoutPrefix.StreamNameBuilder.GenerateForAggregate(typeof(TestAggregate), id);
            Assert.Equal(expectedStreamName, categoryStreamName);
        }

        [Fact]
        public void create_mock_repository_with_a_prefix()
        {
            var prefix = "iamaprefix";
            var id = Guid.NewGuid();
            var expectedStreamName = $"{prefix}.testAggregate-{id:n}";
            var mockRepositoryWithoutPrefix = new MockRepositorySpecification(dataStore: new MockStreamStoreConnection(prefix));
            var categoryStreamName = mockRepositoryWithoutPrefix.StreamNameBuilder.GenerateForAggregate(typeof(TestAggregate), id);
            Assert.Equal(expectedStreamName, categoryStreamName);
        }

        public CommandResponse Handle(TestCommands.Command1 command)
        {
            return command.Succeed();
        }
    }
}
