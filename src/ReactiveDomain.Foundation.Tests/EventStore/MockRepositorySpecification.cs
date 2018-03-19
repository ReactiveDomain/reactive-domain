using ReactiveDomain.Foundation.EventStore;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Messaging.Testing;

namespace ReactiveDomain.Foundation.Tests.EventStore
{
    public abstract class MockRepositorySpecification : CommandBusSpecification
    {
        protected InMemoryBus Bus;
        protected MockEventStoreRepository MockRepository;
        protected ISubscriber MockSubscriber;
        protected ICatchupStreamSubscriber MockListener;

        public IRepository Repository => MockRepository;
        public TestQueue RepositoryQueue;
        public ConcurrentMessageQueue<DomainEvent> RepositoryEvents => RepositoryQueue.Events;

        protected override void Given()
        {
            Bus = new InMemoryBus("In memory bus");
            MockSubscriber = new MockSubscriber(Bus);
            MockRepository = new MockEventStoreRepository(new StreamNameBuilder("UnitTest"), Bus);
            MockListener = new MockCatchupStreamSubscriber(Bus, MockRepository.Store, MockRepository.History);
            RepositoryQueue = new TestQueue(MockSubscriber);
        }

        public virtual void ClearQueues()
        {
            RepositoryQueue.Clear();
            TestQueue.Clear();
        }
    }
}
