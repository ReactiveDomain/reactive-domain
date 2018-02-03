using ReactiveDomain.Messaging.Testing;

namespace ReactiveDomain.Messaging.Tests.Subscribers.QueuedSubscriber
{
    // ReSharper disable InconsistentNaming
    public abstract class when_using_queued_subscriber : CommandBusSpecification
    {
        protected TestQueuedSubscriber MessageSubscriber;

        protected override void Given()
        {
            MessageSubscriber = new TestQueuedSubscriber(Bus);
        }

    }
}
