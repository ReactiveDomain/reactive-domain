using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing.EventStore;

namespace ReactiveDomain.Testing
{
    public class MockRepositorySpecification : DispatcherSpecification
    {
        protected readonly IRepository MockRepository;
        public IRepository Repository => MockRepository;
        public readonly TestQueue RepositoryEvents;

        public MockRepositorySpecification()
        {
            var streamNameBuilder = new PrefixedCamelCaseStreamNameBuilder("UnitTest");
            var mockStreamStore = new MockStreamStoreConnection("Test");
            mockStreamStore.Connect();
            var eventSerializer = new JsonMessageSerializer();
            MockRepository = new StreamStoreRepository(streamNameBuilder, mockStreamStore, eventSerializer);

            var connectorBus = new InMemoryBus("connector");
            mockStreamStore.SubscribeToAll(evt => connectorBus.Publish((IMessage)eventSerializer.Deserialize(evt)));
            RepositoryEvents = new TestQueue(connectorBus,new []{typeof(Event) });
        }

        public virtual void ClearQueues()
        {
            RepositoryEvents.Clear();
            TestQueue.Clear();
        }
    }
}
