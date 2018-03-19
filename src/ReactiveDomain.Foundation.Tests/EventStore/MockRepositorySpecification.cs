using ReactiveDomain.Foundation.EventStore;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Messaging.Testing;

namespace ReactiveDomain.Foundation.Tests.EventStore
{
    public abstract class MockRepositorySpecification : CommandBusSpecification
    {
        
        private MockEventStoreRepository _mockRepository;

        public IRepository Repository => _mockRepository;
        public TestQueue RepositoryQueue;
        public ConcurrentMessageQueue<DomainEvent> RepositoryEvents => RepositoryQueue.Events;

        protected override void Given()
        {
            var bus = new InMemoryBus("Repository out bus");
            _mockRepository = new MockEventStoreRepository(new PrefixedCamelCaseStreamNameBuilder("UnitTest"), bus);
            RepositoryQueue = new TestQueue(bus);
        }

        public virtual void ClearQueues()
        {
            RepositoryQueue.Clear();
            TestQueue.Clear();
        }
    }
}
