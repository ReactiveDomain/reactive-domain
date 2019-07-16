namespace ReactiveDomain.Messaging.Bus
{
    /// <inheritdoc cref="ICommandPublisher"/>
    /// <inheritdoc cref="ICommandSubscriber"/>
    public interface ICommandBus : ICommandPublisher, ICommandSubscriber
    {
    }
}
