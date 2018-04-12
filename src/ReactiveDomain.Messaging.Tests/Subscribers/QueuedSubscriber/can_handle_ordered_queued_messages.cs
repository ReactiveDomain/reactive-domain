using System;
using System.Threading.Tasks;
using ReactiveDomain.Messaging.Messages;
using ReactiveDomain.Messaging.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests.Subscribers.QueuedSubscriber
{
    // ReSharper disable once InconsistentNaming
    public sealed class can_handle_ordered_queued_messages : when_using_counted_message_subscriber
    {
        private int FirstTaskMax = 1000;
        private readonly Task _t1;
        private readonly Task _t2;

        public can_handle_ordered_queued_messages()
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
                    CorrelatedMessage source = CorrelatedMessage.NewRoot();
                    for (int i = 0; i < FirstTaskMax; i++) {
                        var evt = new CountedEvent(i, source);
                        Bus.Publish(evt);
                        source = evt;
                    }
                });
        }

        [Fact]
        void can_handle_messages_in_order()
        {
            _t1.Start();
            Assert.IsOrBecomesTrue(() => _t1.IsCompleted);
            Assert.IsOrBecomesTrue(
                () => MsgCount == FirstTaskMax,
                FirstTaskMax*2,
                $"Expected message count to be {FirstTaskMax} Messages, found {MsgCount }");

            Assert.IsOrBecomesTrue(
                () => MsgCount == FirstTaskMax,
                timeout: FirstTaskMax,
                msg: $"Expected {FirstTaskMax} Messages, found {MsgCount}");
        }

        [Fact]
        void can_handle_events_in_order()
        {
            _t2.Start();
            Assert.IsOrBecomesTrue(() => _t2.IsCompleted);
            Assert.IsOrBecomesTrue(
                () => MsgCount == FirstTaskMax,
                FirstTaskMax,
                $"Expected message count to be {FirstTaskMax} Messages, found {MsgCount }");

            Assert.IsOrBecomesTrue(
                () => MsgCount == FirstTaskMax,
                FirstTaskMax,
                $"Expected message count to be {FirstTaskMax} Messages, found {MsgCount }");
        }


    }
}
