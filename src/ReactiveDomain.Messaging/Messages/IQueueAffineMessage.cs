namespace ReactiveDomain.Messaging.Messages
{
    public interface IQueueAffineMessage
    {
        int QueueId { get; }
    }
}