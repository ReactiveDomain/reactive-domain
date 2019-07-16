using System.Diagnostics;
using System.Threading;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Messaging
{
    public class PublishEnvelope : IEnvelope
    {
        private readonly IPublisher _publisher;
        private readonly int _createdOnThread;

        public PublishEnvelope(IPublisher publisher, bool crossThread = false) 
        {
            _publisher = publisher;
            _createdOnThread = crossThread ? -1 : Thread.CurrentThread.ManagedThreadId;
        }

        public void ReplyWith<T>(T message) where T : IMessage
        {
            Debug.Assert(_createdOnThread == -1 || 
                Thread.CurrentThread.ManagedThreadId == _createdOnThread || _publisher is IThreadSafePublisher);
            _publisher.Publish(message);
        }
    }
}