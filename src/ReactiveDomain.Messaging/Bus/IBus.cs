namespace ReactiveDomain.Messaging.Bus
{
    public interface IBus: IPublisher, ISubscriber
    {
        string Name { get; }
    }
}