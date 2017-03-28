using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ReactiveDomain.Messaging;
using ReactiveDomain.Tests.Helpers;
using Xunit;

namespace ReactiveDomain.Tests.Subscribers.QueuedSubscriber
{
    public class can_handle_inherited_messages : when_using_inherited_queued_subscriber
    {
       
        protected Guid TestMessageId;
        protected Guid TestCorrelationId;
        protected Guid ParentMsgId;
        protected Guid ChildMsgId;

        protected override void When()
        {
       
            TestCorrelationId = Guid.NewGuid();
            var msg = new TestDomainEvent(TestCorrelationId, Guid.Empty);
            TestMessageId = msg.MsgId;
            Bus.Publish(msg);

            var msg2 = new ParentTestDomainEvent(TestCorrelationId,TestMessageId);
            ParentMsgId = msg2.MsgId;
            Bus.Publish(msg2);
            Assert.Equal(BusMessages.Count, 2);

            var msg3 = new ChildTestDomainEvent(TestCorrelationId,ParentMsgId);
            ChildMsgId = msg3.MsgId;
            Bus.Publish(msg3);
            Assert.Equal(BusMessages.Count, 3);

            var msg4 = new GrandChildTestDomainEvent(TestCorrelationId, ChildMsgId);
            Bus.Publish(msg4);
            Assert.Equal(BusMessages.Count, 4);
        }



        [Fact]//(Skip = "test is broken")]
        public void message_handle_invocations_are_correct()
        {
           BusMessages
                .AssertNext<TestDomainEvent>(TestCorrelationId)
                .AssertNext<ParentTestDomainEvent>(TestCorrelationId)
                .AssertNext<ChildTestDomainEvent>(TestCorrelationId)
                .AssertNext<GrandChildTestDomainEvent>(TestCorrelationId)
                .AssertEmpty();

            Assert.IsOrBecomesTrue(
               () => Interlocked.Read(ref MessageSubscriber.TestDomainEventHandleCount) == 1,
               1000,
               $"Expected 1 Test Domain Event Handled, found {MessageSubscriber.TestDomainEventHandleCount}");

            Assert.IsOrBecomesTrue(
               () => Interlocked.Read(ref MessageSubscriber.GrandChildTestDomainEventHandleCount) == 1,
               1000,
               $"Expected 1 GrandChildTestDomainEvent handled , found {MessageSubscriber.ChildTestDomainEventHandleCount}");

            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref MessageSubscriber.ChildTestDomainEventHandleCount) == 2,
                1000,
                $"Expected 1 ChildTestDomainEvent handled , found {MessageSubscriber.ChildTestDomainEventHandleCount}");

            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref MessageSubscriber.ParentTestDomainEventHandleCount) == 3,
                1000,
                $"Expected 2 Parent Test Domain Event handled, found {MessageSubscriber.ParentTestDomainEventHandleCount}");

           
        }
    }
}
