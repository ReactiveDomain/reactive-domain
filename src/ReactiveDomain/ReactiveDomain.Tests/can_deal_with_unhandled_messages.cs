using System;
using System.Threading;
using System.Threading.Tasks;
using ReactiveDomain.Tests.Helpers;
using Xunit;

namespace ReactiveDomain.Tests
{
    // ReSharper disable once InconsistentNaming
    public class can_deal_with_unhandled_messages : when_using_counted_message_subscriber
    {
        private int FirstTaskMax = 1000000;
        private int TimeoutInMs = 5000;

        private Task _t1;
        private Task _t2;

        protected override void When()
        {
            _t1 = new Task(
                () =>
                {
                    for (int i = 0; i < FirstTaskMax; i++)
                    {
                        Bus.Publish(new CountedTestMessage(i));
                        Bus.Publish(new CountedEvent(i, Guid.NewGuid(), Guid.Empty));
                        Bus.Publish(new TestMessage());
                    }
                });

            _t2 = new Task(
                () =>
                {
                    for (int i = 0; i < FirstTaskMax; i++)
                    {
                        Bus.Publish(new CountedTestMessage(i));
                        Bus.Publish(new CountedEvent(i, Guid.NewGuid(), Guid.Empty));
                        Bus.Publish(new TestMessage());
                    }
                });
        }

        [Fact]
        void can_ignore_messages_not_subscribed()
        {
            _t1.Start();

            // The task publishes a CountedTestMessage, a TestMessage and a CountedEvent each time thru the loop, so expect 3 per iteration
            Assert.IsOrBecomesTrue(() => BusMessages.Count == FirstTaskMax * 3, TimeoutInMs, $"Expected {FirstTaskMax * 3} Messages, found {BusMessages.Count}");

            // the task only publishes one Event (CountedEvent), so expect 1 per iteration
            Assert.IsOrBecomesTrue(() => BusEvents.Count == FirstTaskMax , TimeoutInMs, $"Expected {FirstTaskMax} Messages, found {BusEvents.Count}");

            // _messageSubscriber subscribes to CountedTestMessage, but not TestMessage, so expect 1 per iteration
            Assert.IsOrBecomesTrue(
                // ReSharper disable once AccessToDisposedClosure
                () => Interlocked.Read(ref _messageSubscriber.MessagesHandled) == FirstTaskMax,
                2000,
                $"Expected {FirstTaskMax} Messages, found {_messageSubscriber.MessagesHandled}");

            Assert.Equal(BusMessages.Count, FirstTaskMax*3);

            Assert.True(_messageSubscriber.MessagesInOrder(), "Messages are not in order");

            Assert.True(_messageSubscriber.EventsInOrder(), "Events are not in order");

            _messageSubscriber.Dispose();

            TestQueue.Clear();

            _t2.Start();    // publish more messages - no subscriber is available

            //Messages and events are published. Don't know how to prove no one handles them.
            Assert.IsOrBecomesTrue(() => BusMessages.Count == FirstTaskMax * 3, TimeoutInMs, $"Expected {FirstTaskMax * 3} Messages, found {BusMessages.Count}");
        }
    }
}
