namespace ReactiveDomain.Messaging.Bus
{
    public interface IPublisher
    {
        /// <summary>
        /// Publishes a Message
        /// Does not block
        /// </summary>
        /// <param name="message">the message to publish</param>
        void Publish(IMessage message);
    }

    /// <summary>
    /// Marks <see cref="IPublisher"/> as being OK for
    /// cross-thread publishing (e.g. in replying to envelopes).
    /// </summary>
    public interface IThreadSafePublisher 
    {
    }
}
