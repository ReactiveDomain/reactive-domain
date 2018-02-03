using System;
using System.Threading;
using System.Threading.Tasks;
using ReactiveDomain.Messaging.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests.Subscribers.QueuedSubscriber
{
    // ReSharper disable once InconsistentNaming
    public class can_unsubscribe_queued_messages : when_using_counted_message_subscriber
    {
        private int FirstTaskMax = 1000;
        private int SecondTaskMax = 500;
        private int ThirdTaskMax = 500;

        private Task _t1;
        private Task _t2;
        private Task _t3;
        private Task _t4;
        private Task _t5;
        private Task _t6;

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

            _t5 = new Task(
                () =>
                {
                    for (int i = 0; i < SecondTaskMax; i++)
                    {
                        Bus.Publish(new CountedEvent(i, Guid.NewGuid(), Guid.Empty));
                    }
                });
            _t6 = new Task(
                () =>
                {
                    for (int i = 0; i < ThirdTaskMax; i++)
                    {
                        Bus.Publish(new CountedEvent(i, Guid.NewGuid(), Guid.Empty));
                    }
                });
        }

        [Fact]
        void can_unsubscribe_messages_by_disposing()
        {
            _t1.Start();

            Assert.IsOrBecomesTrue(
                // ReSharper disable once AccessToDisposedClosure
                () => Interlocked.Read(ref _messageSubscriber.MessagesHandled) == FirstTaskMax,
                2000,
                $"Expected {FirstTaskMax} Messages, found {_messageSubscriber.MessagesHandled}");

            Assert.Equal(BusMessages.Count, FirstTaskMax);
            Assert.True(_messageSubscriber.MessagesInOrder(), "Messages are not in order");

            _messageSubscriber.Dispose();

            TestQueue.Clear();

            var newSubscriber = new CountedMessageSubscriber(Bus);
            _t2.Start();

            Assert.IsOrBecomesTrue(() => BusMessages.Count == SecondTaskMax, null, $"Expected {FirstTaskMax} Messages, found {BusMessages.Count}");
            Assert.IsOrBecomesTrue(
                // ReSharper disable once AccessToDisposedClosure
                () => Interlocked.Read(ref newSubscriber.MessagesHandled) == SecondTaskMax,
                SecondTaskMax,
                $"Expected {SecondTaskMax} Messages, found {newSubscriber.MessagesHandled}");

            // disposed first subscriber has unsubscribed from messages, and doesn't handle second task messages.
            Assert.Equal(_messageSubscriber.MessagesHandled, FirstTaskMax);

            TestQueue.Clear();

            var thirdSubscriber = new CountedMessageSubscriber(Bus);
            _t3.Start();

            Assert.IsOrBecomesTrue(() => BusMessages.Count == ThirdTaskMax, null, $"Expected {ThirdTaskMax} Messages, found {BusMessages.Count}");
            Assert.IsOrBecomesTrue(
                // ReSharper disable once AccessToDisposedClosure
                () => Interlocked.Read(ref newSubscriber.MessagesHandled) == (SecondTaskMax + ThirdTaskMax),
                ThirdTaskMax,
                $"Expected {(SecondTaskMax + ThirdTaskMax)} Messages, found {newSubscriber.MessagesHandled}");

            Assert.IsOrBecomesTrue(
                // ReSharper disable once AccessToDisposedClosure
                () => Interlocked.Read(ref thirdSubscriber.MessagesHandled) == ThirdTaskMax,
                null,
                $"Expected {ThirdTaskMax} Messages, found {newSubscriber.MessagesHandled}");

            Assert.Equal(newSubscriber.MessagesHandled, (SecondTaskMax + ThirdTaskMax));
            Assert.Equal(thirdSubscriber.MessagesHandled, ThirdTaskMax);

        }

        [Fact]
        void can_unsubscribe_events_by_disposing()
        {
            _t4.Start();

            Assert.IsOrBecomesTrue(
                // ReSharper disable once AccessToDisposedClosure
                () => Interlocked.Read(ref _messageSubscriber.EventsHandled) == FirstTaskMax,
                2000,
                $"Expected {FirstTaskMax} events, found {_messageSubscriber.EventsHandled}");

            Assert.IsOrBecomesTrue(
                // ReSharper disable once AccessToDisposedClosure
                () => BusEvents.Count == FirstTaskMax,
                null,
                $"Expected {FirstTaskMax} events, found {_messageSubscriber.EventsHandled}");

            Assert.Equal(BusEvents.Count, FirstTaskMax);
            Assert.True(_messageSubscriber.EventsInOrder(), "Messages are not in order");

            _messageSubscriber.Dispose();

            TestQueue.Clear();

            var newSubscriber = new CountedMessageSubscriber(Bus);
            _t5.Start();

            Assert.IsOrBecomesTrue(
                () => BusEvents.Count == SecondTaskMax, 
                2000, 
                $"Expected {SecondTaskMax} events, found {BusEvents.Count}");

            Assert.IsOrBecomesTrue(
                // ReSharper disable once AccessToDisposedClosure
                () => Interlocked.Read(ref newSubscriber.EventsHandled) == SecondTaskMax,
                null,
                $"Expected {SecondTaskMax} events, found {newSubscriber.EventsHandled}");

            // disposed first subscriber has unsubscribed from messages, and doesn't handle second task messages.
            Assert.Equal(_messageSubscriber.EventsHandled, FirstTaskMax);

            TestQueue.Clear();

            var thirdSubscriber = new CountedMessageSubscriber(Bus);
            _t6.Start();

            Assert.IsOrBecomesTrue(() => BusEvents.Count == ThirdTaskMax, null, $"Expected {ThirdTaskMax} events, found {BusEvents.Count}");
            Assert.IsOrBecomesTrue(
                // ReSharper disable once AccessToDisposedClosure
                () => Interlocked.Read(ref newSubscriber.EventsHandled) == (SecondTaskMax + ThirdTaskMax),
                null,
                $"Expected {(SecondTaskMax + ThirdTaskMax)} events, found {newSubscriber.EventsHandled}");

            Assert.IsOrBecomesTrue(
                // ReSharper disable once AccessToDisposedClosure
                () => Interlocked.Read(ref thirdSubscriber.EventsHandled) == ThirdTaskMax,
                null,
                $"Expected {ThirdTaskMax} events, found {newSubscriber.EventsHandled}");

            Assert.Equal(newSubscriber.EventsHandled, (SecondTaskMax + ThirdTaskMax));
            Assert.Equal(thirdSubscriber.EventsHandled, ThirdTaskMax);

        }
    }
}
