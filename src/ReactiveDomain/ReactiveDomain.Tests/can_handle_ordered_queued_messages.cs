using System;
using System.Threading;
using System.Threading.Tasks;
using ReactiveDomain.Tests.Helpers;
using Xunit;

namespace ReactiveDomain.Tests
{
    // ReSharper disable once InconsistentNaming
    public class can_handle_ordered_queued_messages : when_using_counted_message_subscriber
    {
        private int FirstTaskMax = 1000;
        private int SecondTaskMax = 500;
        private int ThirdTaskMax = 500;

        private Task _t1;
        private Task _t2;
        private Task _t3;
        private Task _t4;

        protected override void When()
        {
            _t1 = new Task(
                () =>
                {
                    for (int i = 0; i < FirstTaskMax; i++)
                    {
                        Bus.Publish(new CountedTestMessage(i));
                    }
                });

            _t2 = new Task(
                () =>
                {
                    for (int i = 0; i < SecondTaskMax; i++)
                    {
                        Bus.Publish(new CountedTestMessage(i));
                    }
                });

            _t3 = new Task(
                () =>
                {
                    for (int i = 0; i < ThirdTaskMax; i++)
                    {
                        Bus.Publish(new CountedTestMessage(i));
                    }
                });

            _t4 = new Task(
                () =>
                {
                    for (int i = 0; i < FirstTaskMax; i++)
                    {
                        Bus.Publish(new CountedEvent(i, Guid.NewGuid(), Guid.Empty));
                    }
                });
        }

        [Fact]
        void can_handle_messages_in_order()
        {
            _t1.Start();

            Assert.IsOrBecomesTrue(
                () => BusMessages.Count == FirstTaskMax,
                FirstTaskMax*2,
                $"Expected message count to be {FirstTaskMax} Messages, found {BusMessages.Count }");

            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref _messageSubscriber.MessagesHandled) == FirstTaskMax,
                timeout: FirstTaskMax,
                msg: $"Expected {FirstTaskMax} Messages, found {_messageSubscriber.MessagesHandled}");

            Assert.True(_messageSubscriber.MessagesInOrder(), "Messages are not in order");
        }

        [Fact]
        void can_handle_events_in_order()
        {
            _t4.Start();

            Assert.IsOrBecomesTrue(
                () => BusEvents.Count == FirstTaskMax,
                FirstTaskMax,
                $"Expected message count to be {FirstTaskMax} Messages, found {BusEvents.Count }");

            Assert.IsOrBecomesTrue(
                () => BusMessages.Count == FirstTaskMax,
                FirstTaskMax,
                $"Expected message count to be {FirstTaskMax} Messages, found {BusMessages.Count }");

            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref _messageSubscriber.EventsHandled) == FirstTaskMax,
                timeout: 2000,
                msg: $"Expected {FirstTaskMax} events, found {_messageSubscriber.EventsHandled}");

            Assert.True(_messageSubscriber.EventsInOrder(), "Events are not in order");
        }


    }
}
