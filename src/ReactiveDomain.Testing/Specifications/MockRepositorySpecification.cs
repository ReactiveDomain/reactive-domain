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
        public IStreamNameBuilder StreamNameBuilder { get; }
        public IStreamStoreConnection StreamStoreConnection { get; }
        public IEventSerializer EventSerializer { get; }
        public string Schema { get; set; } = "Test";

        public MockRepositorySpecification()
        {
            StreamNameBuilder = new PrefixedCamelCaseStreamNameBuilder(Schema);
            StreamStoreConnection = new MockStreamStoreConnection(Schema);
            StreamStoreConnection.Connect();
            EventSerializer = new JsonMessageSerializer();
            MockRepository = new StreamStoreRepository(StreamNameBuilder, StreamStoreConnection, EventSerializer);

            var connectorBus = new InMemoryBus("connector");
            StreamStoreConnection.SubscribeToAll(evt => {
                if(evt is ProjectedEvent) { return;}
                connectorBus.Publish((IMessage) EventSerializer.Deserialize(evt));
            });
            RepositoryEvents = new TestQueue(connectorBus, new[] { typeof(Event) });
        }

        public virtual void ClearQueues()
        {
            RepositoryEvents.Clear();
            TestQueue.Clear();
        }

        public IListener GetListener(string name) =>
                            new QueuedStreamListener(
                                name,
                                StreamStoreConnection,
                                StreamNameBuilder,
                                EventSerializer);
    }
}
