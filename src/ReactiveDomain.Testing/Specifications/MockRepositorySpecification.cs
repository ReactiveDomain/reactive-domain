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
        public IConfiguredConnection ConfiguredConnection { get; }

        private string _schema;
        public string Schema => _schema;

        /// <summary>
        /// Creates a mock repository.
        /// </summary>
        /// <param name="schema">Schema prefix for stream name.</param>
        /// <param name="dataStore">Stream store connection.</param>
        private MockRepositorySpecification(string schema, IStreamStoreConnection dataStore)
        {
            _schema = schema;
            StreamNameBuilder = string.IsNullOrEmpty(schema) ? new PrefixedCamelCaseStreamNameBuilder() : new PrefixedCamelCaseStreamNameBuilder(schema);
            StreamStoreConnection = dataStore;
            StreamStoreConnection.Connect();
            EventSerializer = new JsonMessageSerializer();
            MockRepository = new StreamStoreRepository(StreamNameBuilder, StreamStoreConnection, EventSerializer);

            ConfiguredConnection = new ConfiguredConnection(StreamStoreConnection, StreamNameBuilder, EventSerializer);

            var connectorBus = new InMemoryBus("connector");
            StreamStoreConnection.SubscribeToAll(evt =>
            {
                if (evt is ProjectedEvent) { return; }
                connectorBus.Publish((IMessage)EventSerializer.Deserialize(evt));
            });
            RepositoryEvents = new TestQueue(connectorBus, new[] { typeof(Event) });
        }

        /// <summary>
        /// Creates a mock repository with a prefix.
        /// </summary>
        /// <param name="schema">Schema prefix for stream name. Default value is "Test".</param>
        public MockRepositorySpecification(string schema = "Test") : this(schema, new MockStreamStoreConnection(schema))
        {
        }

        /// <summary>
        /// Creates a mock repository connected to a StreamStore. 
        /// </summary>
        /// <param name="dataStore">Stream store connection.</param>
        public MockRepositorySpecification(IStreamStoreConnection dataStore) : this(dataStore.ConnectionName, dataStore)
        {
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
