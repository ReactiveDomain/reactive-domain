using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Messaging.Tests;
using ReactiveDomain.Messaging.Tests.Helpers;
using ReactiveDomain.Messaging.Tests.Specifications;

namespace ReactiveDomain.Foundation.Tests.EventStore
{
    public abstract class MockRepositorySpecification : CommandBusSpecification
    {
        protected MockEventStoreRepository MockRepository;
        public IRepository Repository => MockRepository;
        public TestQueue RepositoryQueue;
        public ConcurrentMessageQueue<DomainEvent> RepositoryEvents => RepositoryQueue.Events;

        protected override void Given()
        {

            MockRepository = new MockEventStoreRepository();
            RepositoryQueue = new TestQueue((ISubscriber)Repository);
        }

        public virtual void ClearQueues()
        {
            RepositoryQueue.Clear();
            TestQueue.Clear();
        }
    }
}
