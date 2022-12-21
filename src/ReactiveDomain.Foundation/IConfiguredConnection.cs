using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using System;

namespace ReactiveDomain.Foundation
{
    public interface IConfiguredConnection
    {
        IStreamStoreConnection Connection { get; }
        IStreamNameBuilder StreamNamer { get; }
        IEventSerializer Serializer { get; }
        IListener GetListener(string name);
        IListener GetQueuedListener(string name);
        IStreamReader GetReader(string name, Action<IMessage> handle);
        IRepository GetRepository(bool caching = false, Func<Guid> currentPolicyUserId = null);
        ICorrelatedRepository GetCorrelatedRepository(IRepository baseRepository = null, bool caching = false, Func<Guid> currentPolicyUserId = null);

    }
}
