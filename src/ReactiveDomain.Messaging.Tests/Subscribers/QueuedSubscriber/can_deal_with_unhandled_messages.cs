using System;
using System.Threading;
using System.Threading.Tasks;
using ReactiveDomain.Messaging.Messages;
using ReactiveDomain.Messaging.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests.Subscribers.QueuedSubscriber
{
    // ReSharper disable once InconsistentNaming
    public sealed class can_deal_with_unhandled_messages : when_using_counted_message_subscriber
    {
        private int FirstTaskMax = 20;
        private int TimeoutInMs = 5000;
       
        [Fact]
        void can_ignore_messages_not_subscribed()
        {
            CorrelatedMessage source = CorrelatedMessage.NewRoot();
            for (int i = 0; i < FirstTaskMax; i++)
            {
                Bus.Publish(new CountedTestMessage(i));
                var evt = new CountedEvent(i, source);
                Bus.Publish(evt);
                Bus.Publish(new TestMessage());
                source = evt;
            }

            // The task publishes a CountedTestMessage, a TestMessage and a CountedEvent each time thru the loop, so expect 3 per iteration
            Assert.IsOrBecomesTrue(() => MsgCount == FirstTaskMax * 3, TimeoutInMs, $"Expected {FirstTaskMax * 3} Messages, found {MsgCount}");

            // the task only publishes one Event (CountedEvent), so expect 1 per iteration
            Assert.IsOrBecomesTrue(() => EventCount == FirstTaskMax , TimeoutInMs, $"Expected {FirstTaskMax} Messages, found {EventCount}");

            // _messageSubscriber subscribes to CountedTestMessage, but not TestMessage, so expect 1 per iteration
            Assert.IsOrBecomesTrue(()=>
                 TestMsgCount == FirstTaskMax,
                2000,
                $"Expected {FirstTaskMax} Messages, found {TestMsgCount}");

            Assert.Equal(MsgCount, FirstTaskMax*3);

           // Assert.True(IsInOrder);
         

            Clear();

            for (int i = 0; i < FirstTaskMax; i++)
            {
                Bus.Publish(new CountedTestMessage(i));
                var evt = new CountedEvent(i, source);
                Bus.Publish(evt);
                Bus.Publish(new TestMessage());
                source = evt;
            }  // publish more messages - no subscriber is available

            //Messages and events are published. Don't know how to prove no one handles them.
            Assert.IsOrBecomesTrue(() => MsgCount == FirstTaskMax * 3, 100, $"Expected {FirstTaskMax * 3} Messages, found {MsgCount}");
        }
     
    }
}
