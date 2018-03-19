using ReactiveDomain.Foundation.EventStore;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Messaging.Testing;

namespace ReactiveDomain.Foundation.Tests.EventStore
{
    public abstract class MockRepositorySpecification : CommandBusSpecification
    {
        protected InMemoryBus Bus;
        protected MockSubscriber Mockubscriber;
        protected MockEventStoreRepository MockRepository;

        public IRepository Repository => MockRepository;
        public TestQueue RepositoryQueue;
        public ConcurrentMessageQueue<DomainEvent> RepositoryEvents => RepositoryQueue.Events;

        protected override void Given()
        {
            Bus = new InMemoryBus("In memory bus");
            Mockubscriber = new MockSubscriber(Bus);
            MockRepository = new MockEventStoreRepository(new StreamNameBuilder("UnitTest"), Bus);
            RepositoryQueue = new TestQueue(Mockubscriber);
        }

        public virtual void ClearQueues()
        {
            RepositoryQueue.Clear();
            TestQueue.Clear();
        }
    }
}
