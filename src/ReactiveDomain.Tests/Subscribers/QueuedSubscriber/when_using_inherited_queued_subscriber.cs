using ReactiveDomain.Tests.Specifications;

namespace ReactiveDomain.Tests.Subscribers.QueuedSubscriber
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
