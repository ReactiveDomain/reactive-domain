namespace ReactiveDomain.Messaging.Bus
{
    /// <inheritdoc cref="IPublisher"/>
    /// <inheritdoc cref="ISubscriber"/>
    public interface IBus: IPublisher, ISubscriber
    {
        /// <summary>
        /// Name of the Bus, used in logging and stats reporting
        /// </summary>
        string Name { get; }
    }
}