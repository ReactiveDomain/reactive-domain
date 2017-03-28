using ReactiveDomain.Tests.Helpers;
using ReactiveDomain.Tests.Specifications;
using ReactiveDomain.Tests.Subscribers.QueuedSubscriber;

namespace ReactiveDomain.Tests
{
    // ReSharper disable InconsistentNaming
    public abstract class when_using_counted_message_subscriber : CommandBusSpecification
    {
        protected CountedMessageSubscriber _messageSubscriber;

        protected override void Given()
        {
            _messageSubscriber = new CountedMessageSubscriber(Bus);
        }

    }
}