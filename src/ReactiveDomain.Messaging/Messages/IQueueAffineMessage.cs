namespace ReactiveDomain.Messaging
{
    public interface IQueueAffineMessage : IMessage
    {
        int QueueId { get; }
    }
}