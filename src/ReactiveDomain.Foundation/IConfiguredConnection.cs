namespace ReactiveDomain.Foundation
{
    public interface IConfiguredConnection
    {
        IStreamStoreConnection Connection { get; }
        IStreamNameBuilder StreamNamer { get; }
        IEventSerializer Serializer { get; }
        IListener GetListener(string name);
        IListener GetQueuedListener(string name);
        IStreamReader GetReader(string name);
    }
}
