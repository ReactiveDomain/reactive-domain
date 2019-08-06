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
        public IStreamStoreConnection StreamStoreConnection { get; }

        public MockRepositorySpecification()
        {
            var streamNameBuilder = new PrefixedCamelCaseStreamNameBuilder("UnitTest");
            StreamStoreConnection = new MockStreamStoreConnection("Test");
            StreamStoreConnection.Connect();
            var eventSerializer = new JsonMessageSerializer();
            MockRepository = new StreamStoreRepository(streamNameBuilder, StreamStoreConnection, eventSerializer);

            var connectorBus = new InMemoryBus("connector");
            StreamStoreConnection.SubscribeToAll(evt => connectorBus.Publish((IMessage)eventSerializer.Deserialize(evt)));
            RepositoryEvents = new TestQueue(connectorBus,new []{typeof(Event) });
        }

        public virtual void ClearQueues()
        {
            RepositoryEvents.Clear();
            TestQueue.Clear();
        }
    }
}
