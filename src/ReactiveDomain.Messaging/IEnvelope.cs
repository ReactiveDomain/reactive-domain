namespace ReactiveDomain.Messaging
{
    public interface IEnvelope
    {
        void ReplyWith<T>(T message) where T : IMessage;
    }
}