using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests.Subscribers.QueuedSubscriber
{
    // ReSharper disable once InconsistentNaming
    public sealed class can_handle_high_volume_queued_messages : when_using_queued_subscriber
    {
        private TestMessagePublisher _pub1;
        private TestMessagePublisher _pub2;
        private TestMessagePublisher _pub3;
        private TestMessagePublisher _pub4;
        private int FirstTaskMax = 50000;
        private int TimeoutInMs = 50000;

       public can_handle_high_volume_queued_messages()
        {
            // create multiple publishers
            _pub1 = new TestMessagePublisher(Bus);
            _pub2 = new TestMessagePublisher(Bus);
            _pub3 = new TestMessagePublisher(Bus);
            _pub4 = new TestMessagePublisher(Bus);
        }

        [Fact]
        void can_handle_multiple_publishers()
        {
            // start publishers
            _pub1.StartPublishing(0);
            _pub2.StartPublishing(0);
            _pub3.StartPublishing(0);
            _pub4.StartPublishing(0);

            // When we get to (or beyond) a predetermined number of messages published...
            Assert.IsOrBecomesTrue(
                () => MsgCount > FirstTaskMax,
                TimeoutInMs,
                $"Expected message count to be {FirstTaskMax} Messages, found {MsgCount}");

            // ... stop the publishers
            _pub1.StopPublishing();
            _pub2.StopPublishing();
            _pub3.StopPublishing();
            _pub4.StopPublishing();

            // verify all the messages were handled
            Assert.IsOrBecomesTrue(() => MessageSubscriber.TimesTestMessageHandled == MsgCount,
                TimeoutInMs,
                $"Subscriber handled ParentTest message {MessageSubscriber.ParentTestMessage} times. Bus count = {MsgCount}");

        }
    }
}
