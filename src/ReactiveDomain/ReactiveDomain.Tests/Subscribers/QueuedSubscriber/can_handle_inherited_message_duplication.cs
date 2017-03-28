using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ReactiveDomain.Messaging;
using ReactiveDomain.Tests.Helpers;
using ReactiveDomain.Tests.Specifications;
using Xunit;

namespace ReactiveDomain.Tests.Subscribers.QueuedSubscriber
{
    public class can_handle_inherited_message_duplication : CommandBusSpecification
    {
       
        protected Guid TestMessageId;
        protected Guid TestCorrelationId;
        protected Guid ParentMsgId;
        protected Guid ChildMsgId;

        protected TestInheritedMessageSubscriber MessageSubscriber;

        protected override void Given()
        {
            //n.b the subscriber subscribes to the following
            // Subscribe<TestDomainEvent>(this);
            //Subscribe<ParentTestDomainEvent>(this);
            //Subscribe<ChildTestDomainEvent>(this);
            //Subscribe<GrandChildTestDomainEvent>(this);
            MessageSubscriber = new TestInheritedMessageSubscriber(Bus,false);
        }
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
        [Fact]
        public void grand_child_invokes_all_handlers_four_times()
        {
            //n.b. the number of duplications matched the number of times we have subscribed to the hierarchy
            //see the next test where we do not subscribe to Message
            // the subscription to Test Event does not matter because it tis outside the heirarchy

            MessageSubscriber.Subscribe<Message>(MessageSubscriber);
            MessageSubscriber.Reset();
            TestQueue.Clear();
            Bus.Publish(new GrandChildTestDomainEvent(TestCorrelationId, ChildMsgId));

            BusMessages
               .AssertNext<GrandChildTestDomainEvent>(TestCorrelationId)
               .AssertEmpty();

            Assert.IsOrBecomesTrue(
               () => Interlocked.Read(ref MessageSubscriber.TestDomainEventHandleCount) == 0,
               1000,
               $"Expected 0 Test Domain Event Handled, found {MessageSubscriber.TestDomainEventHandleCount}");

            Assert.IsOrBecomesTrue(
               () => Interlocked.Read(ref MessageSubscriber.GrandChildTestDomainEventHandleCount) == 4,
               1000,
               $"Expected 3 GrandChildTestDomainEvent handled , found {MessageSubscriber.ChildTestDomainEventHandleCount}");

            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref MessageSubscriber.ChildTestDomainEventHandleCount) == 4,
                1000,
                $"Expected 3 ChildTestDomainEvent handled , found {MessageSubscriber.ChildTestDomainEventHandleCount}");

            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref MessageSubscriber.ParentTestDomainEventHandleCount) == 4,
                1000,
                $"Expected 3 Parent Test Domain Event handled, found {MessageSubscriber.ParentTestDomainEventHandleCount}");
            Assert.IsOrBecomesTrue(
               () => Interlocked.Read(ref MessageSubscriber.MessageHandleCount) == 4,
               1000,
               $"Expected 3 Parent Test Domain Event handled, found {MessageSubscriber.MessageHandleCount}");
        }
        [Fact]
        public void grand_child_invokes_all_handlers_thrice()
        {
            MessageSubscriber.Reset();
            TestQueue.Clear();
            Bus.Publish(new GrandChildTestDomainEvent(TestCorrelationId, ChildMsgId));

            BusMessages
               .AssertNext<GrandChildTestDomainEvent>(TestCorrelationId)
               .AssertEmpty();

            Assert.IsOrBecomesTrue(
               () => Interlocked.Read(ref MessageSubscriber.TestDomainEventHandleCount) == 0,
               1000,
               $"Expected 0 Test Domain Event Handled, found {MessageSubscriber.TestDomainEventHandleCount}");

            Assert.IsOrBecomesTrue(
               () => Interlocked.Read(ref MessageSubscriber.GrandChildTestDomainEventHandleCount) == 3,
               1000,
               $"Expected 3 GrandChildTestDomainEvent handled , found {MessageSubscriber.ChildTestDomainEventHandleCount}");

            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref MessageSubscriber.ChildTestDomainEventHandleCount) == 3,
                1000,
                $"Expected 3 ChildTestDomainEvent handled , found {MessageSubscriber.ChildTestDomainEventHandleCount}");

            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref MessageSubscriber.ParentTestDomainEventHandleCount) == 3,
                1000,
                $"Expected 3 Parent Test Domain Event handled, found {MessageSubscriber.ParentTestDomainEventHandleCount}");
        }
        [Fact]
        public void child_invokes_parent_and_child_handlers_twice()
        {
            MessageSubscriber.Reset();
            TestQueue.Clear();
            Bus.Publish(new ChildTestDomainEvent(TestCorrelationId, ChildMsgId));

            BusMessages
               .AssertNext<ChildTestDomainEvent>(TestCorrelationId)
               .AssertEmpty();

            Assert.IsOrBecomesTrue(
               () => Interlocked.Read(ref MessageSubscriber.TestDomainEventHandleCount) == 0,
               1000,
               $"Expected 0 Test Domain Event Handled, found {MessageSubscriber.TestDomainEventHandleCount}");

            Assert.IsOrBecomesTrue(
               () => Interlocked.Read(ref MessageSubscriber.GrandChildTestDomainEventHandleCount) == 0,
               1000,
               $"Expected 0 GrandChildTestDomainEvent handled , found {MessageSubscriber.ChildTestDomainEventHandleCount}");

            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref MessageSubscriber.ChildTestDomainEventHandleCount) == 2,
                1000,
                $"Expected 2 ChildTestDomainEvent handled , found {MessageSubscriber.ChildTestDomainEventHandleCount}");

            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref MessageSubscriber.ParentTestDomainEventHandleCount) == 2,
                1000,
                $"Expected 2 Parent Test Domain Event handled, found {MessageSubscriber.ParentTestDomainEventHandleCount}");
        }
        [Fact]
        public void parent_invokes_only_parent_handler_once()
        {
            MessageSubscriber.Reset();
            TestQueue.Clear();

            Bus.Publish(new ParentTestDomainEvent(TestCorrelationId, ChildMsgId));

            BusMessages
               .AssertNext<ParentTestDomainEvent>(TestCorrelationId)
               .AssertEmpty();

            Assert.IsOrBecomesTrue(
               () => Interlocked.Read(ref MessageSubscriber.TestDomainEventHandleCount) == 0,
               1000,
               $"Expected 0 Test Domain Event Handled, found {MessageSubscriber.TestDomainEventHandleCount}");

            Assert.IsOrBecomesTrue(
               () => Interlocked.Read(ref MessageSubscriber.GrandChildTestDomainEventHandleCount) == 0,
               1000,
               $"Expected 0 GrandChildTestDomainEvent handled , found {MessageSubscriber.ChildTestDomainEventHandleCount}");

            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref MessageSubscriber.ChildTestDomainEventHandleCount) == 0,
                1000,
                $"Expected 0 ChildTestDomainEvent handled , found {MessageSubscriber.ChildTestDomainEventHandleCount}");

            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref MessageSubscriber.ParentTestDomainEventHandleCount) == 1,
                1000,
                $"Expected 1 Parent Test Domain Event handled, found {MessageSubscriber.ParentTestDomainEventHandleCount}");
        }

        [Fact]
        public void multiple_duplicate_message_handle_invocations_are_correct()
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
               () => Interlocked.Read(ref MessageSubscriber.GrandChildTestDomainEventHandleCount) == 3,
               1000,
               $"Expected 3 GrandChildTestDomainEvent handled , found {MessageSubscriber.GrandChildTestDomainEventHandleCount}");

            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref MessageSubscriber.ChildTestDomainEventHandleCount) == 5,
                1000,
                $"Expected 5 ChildTestDomainEvent handled , found {MessageSubscriber.ChildTestDomainEventHandleCount}");

            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref MessageSubscriber.ParentTestDomainEventHandleCount) == 6,
                1000,
                $"Expected 6 Parent Test Domain Event handled, found {MessageSubscriber.ParentTestDomainEventHandleCount}");


        }
    }
}
