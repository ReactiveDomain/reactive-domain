using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Foundation
{
    public interface IConfiguredConnection
    {
        IStreamStoreConnection Connection { get; }
        IStreamNameBuilder StreamNamer { get; }
        IEventSerializer Serializer { get; }
        IListener GetListener(string name);
        IListener GetQueuedListener(string name);
        IStreamReader GetReader(string name, IHandle<IMessage> target = null);
        IRepository GetRepository(bool caching = false);
        ICorrelatedRepository GetCorrelatedRepository(IRepository baseRepository = null, bool caching = false);

    }
}
