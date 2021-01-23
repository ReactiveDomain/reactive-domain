using ReactiveDomain.Foundation.StreamStore;

namespace ReactiveDomain.Foundation
{
    public class ConfiguredConnection : IConfiguredConnection
    {

        public ConfiguredConnection(
            IStreamStoreConnection conn,
            IStreamNameBuilder namer,
            IEventSerializer serializer)
        {
            Connection = conn;
            StreamNamer = namer;
            Serializer = serializer;
        }

        public IStreamStoreConnection Connection { get; }
        public IStreamNameBuilder StreamNamer { get; }

        public IEventSerializer Serializer { get; }

        public IListener GetListener(string name)
        {
            return new StreamListener(name, Connection, StreamNamer, Serializer);
        }
        public IListener GetQueuedListener(string name)
        {
            return new QueuedStreamListener(name, Connection, StreamNamer, Serializer);
        }

        public IStreamReader GetReader(string name)
        {
            return new StreamReader(name, Connection, StreamNamer, Serializer);
        }

        public IRepository GetRepository(bool caching = false)
        {
            IRepository repo = new StreamStoreRepository(StreamNamer, Connection, Serializer);
            return caching
                ? new ReadThroughAggregateCache(repo)
                : repo;
        }

        public ICorrelatedRepository GetCorrelatedRepository(
            IRepository baseRepository = null,bool caching = false)
        {
            return new CorrelatedStreamStoreRepository(baseRepository ?? GetRepository(caching));
        }

    }
}