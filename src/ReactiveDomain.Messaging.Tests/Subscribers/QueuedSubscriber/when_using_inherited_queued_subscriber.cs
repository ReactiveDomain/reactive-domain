using ReactiveDomain.Messaging.Testing;

namespace ReactiveDomain.Messaging.Tests.Subscribers.QueuedSubscriber
{
    // ReSharper disable InconsistentNaming
    public abstract class when_using_inherited_queued_subscriber : CommandBusSpecification
    {
        protected TestInheritedMessageSubscriber MessageSubscriber;

        protected override void Given()
        {
            MessageSubscriber = new TestInheritedMessageSubscriber(Bus);
        }

    }
}
