using System.Collections.Generic;
using System.Linq;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing.EventStore;

namespace ReactiveDomain.Testing
{
    public class MultiMockRepositorySpecification : DispatcherSpecification
    {
        private const string DefaultSchema = "Test";
        public readonly TestQueue RepositoryEvents;
        public IStreamStoreConnection StreamStoreConnection { get; }
        public IEventSerializer EventSerializer { get; }
        private readonly Dictionary<string, MockRepoConnection> _mockRepoConnections = new Dictionary<string, MockRepoConnection>();

        public MultiMockRepositorySpecification(HashSet<string> schemas)
        {
            StreamStoreConnection = new MockStreamStoreConnection("Test");
            StreamStoreConnection.Connect();
            EventSerializer = new JsonMessageSerializer();

            if (schemas?.Any() != true) { schemas = new HashSet<string> { DefaultSchema }; }
            foreach (var schema in schemas)
            {
                _mockRepoConnections.Add(schema, new MockRepoConnection(schema, StreamStoreConnection, EventSerializer));
            }

            var connectorBus = new InMemoryBus("connector");
            StreamStoreConnection.SubscribeToAll(evt =>
            {
                if (evt is ProjectedEvent) { return; }
                connectorBus.Publish((IMessage)EventSerializer.Deserialize(evt));
            });
            RepositoryEvents = new TestQueue(connectorBus, new[] { typeof(Event) });
        }

        /// <summary>
        /// Gets a listener for streams with a given schema.
        /// </summary>
        /// <param name="schema">The schema to use as the prefix to the steam name builder for the listener.</param>
        /// <returns>A listener.</returns>
        public IListener GetListener(string schema) => _mockRepoConnections[schema].GetListener(schema);

        public virtual void ClearQueues()
        {
            RepositoryEvents.Clear();
            TestQueue.Clear();
        }
    }

    public class MockRepoConnection
    {
        private readonly IStreamStoreConnection _connection;
        private readonly IEventSerializer _eventSerializer;

        protected readonly IRepository MockRepository;
        public IRepository Repository => MockRepository;
        public IStreamNameBuilder StreamNameBuilder { get; }
        public IConfiguredConnection ConfiguredConnection { get; }
        public string Schema { get; }

        public MockRepoConnection(string schema, IStreamStoreConnection connection, IEventSerializer eventSerializer)
        {
            Schema = schema;
            _connection = connection;
            _eventSerializer = eventSerializer;
            StreamNameBuilder = new PrefixedCamelCaseStreamNameBuilder(Schema);
            MockRepository = new StreamStoreRepository(StreamNameBuilder, connection, eventSerializer);
            ConfiguredConnection = new ConfiguredConnection(connection, StreamNameBuilder, eventSerializer);
        }

        /// <summary>
        /// Gets a listener for events on this repository.
        /// </summary>
        /// <param name="name">The name of the listener.</param>
        /// <returns>A listener.</returns>
        public IListener GetListener(string name) =>
                            new QueuedStreamListener(
                                name,
                                _connection,
                                StreamNameBuilder,
                                _eventSerializer);
    }
}
