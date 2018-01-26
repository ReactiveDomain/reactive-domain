using ReactiveDomain.Legacy.CommonDomain;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Tests.Helpers;
using ReactiveDomain.Tests.Mocks;

namespace ReactiveDomain.Tests.Specifications
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
