using System;
using System.Threading;
using ReactiveDomain.Messaging.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests.Subscribers.QueuedSubscriber
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
            Assert.IsOrBecomesTrue(()=>BusMessages.Count == 1,msg:"Setup Failure: TestDomainEvent");

            var msg2 = new ParentTestDomainEvent(TestCorrelationId,TestMessageId);
            ParentMsgId = msg2.MsgId;
            Bus.Publish(msg2);
            Assert.IsOrBecomesTrue(()=>BusMessages.Count == 2,msg:"Setup Failure: ParentTestDomainEvent");

            var msg3 = new ChildTestDomainEvent(TestCorrelationId,ParentMsgId);
            ChildMsgId = msg3.MsgId;
            Bus.Publish(msg3);
            Assert.IsOrBecomesTrue(()=>BusMessages.Count == 3,msg:"Setup Failure: ChildTestDomainEvent");

            var msg4 = new GrandChildTestDomainEvent(TestCorrelationId, ChildMsgId);
            Bus.Publish(msg4);
            Assert.IsOrBecomesTrue(()=>BusMessages.Count == 4,msg:"Setup Failure: GrandChildTestDomainEvent");

            //used in multiple_message_handle_invocations_are_correct test 
        }

        [Fact]
        public void grand_child_invokes_all_handlers_once()
        {
            using (var sub = new TestInheritedMessageSubscriber(Bus))
            {
                var subscriber = sub;
                TestQueue.Clear();
                Bus.Publish(new GrandChildTestDomainEvent(TestCorrelationId, ChildMsgId));

                BusMessages
                    .AssertNext<GrandChildTestDomainEvent>(TestCorrelationId)
                    .AssertEmpty();

                Assert.IsOrBecomesTrue(() => subscriber.Starving, 3000);

                Assert.IsOrBecomesTrue(
                    () => Interlocked.Read(ref subscriber.TestDomainEventHandleCount) == 0,
                    1000,
                    $"Expected 0 Test Domain Event Handled, found {subscriber.TestDomainEventHandleCount}");

                Assert.IsOrBecomesTrue(
                    () => Interlocked.Read(ref subscriber.GrandChildTestDomainEventHandleCount) == 1,
                    1000,
                    $"Expected 1 GrandChildTestDomainEvent handled , found {subscriber.ChildTestDomainEventHandleCount}");

                Assert.IsOrBecomesTrue(
                    () => Interlocked.Read(ref subscriber.ChildTestDomainEventHandleCount) == 1,
                    1000,
                    $"Expected 1 ChildTestDomainEvent handled , found {subscriber.ChildTestDomainEventHandleCount}");

                Assert.IsOrBecomesTrue(
                    () => Interlocked.Read(ref subscriber.ParentTestDomainEventHandleCount) == 1,
                    1000,
                    $"Expected 1 Parent Test Domain Event handled, found {subscriber.ParentTestDomainEventHandleCount}");
            }
        }

        [Fact]
        public void child_invokes_parent_and_child_handlers_once()
        {
            using (var sub = new TestInheritedMessageSubscriber(Bus))
            {
                var subscription = sub;
                TestQueue.Clear();
                Bus.Publish(new ChildTestDomainEvent(TestCorrelationId, ChildMsgId));

                BusMessages
                    .AssertNext<ChildTestDomainEvent>(TestCorrelationId)
                    .AssertEmpty();

                Assert.IsOrBecomesTrue(() => subscription.Starving, 3000);

                Assert.IsOrBecomesTrue(() => subscription.TestDomainEventHandleCount == 0,
                    1000,
                    $"Expected 0 Test Domain Event Handled, found {subscription.TestDomainEventHandleCount}");

                Assert.IsOrBecomesTrue(
                    () => Interlocked.Read(ref subscription.GrandChildTestDomainEventHandleCount) == 0,
                    1000,
                    $"Expected 0 GrandChildTestDomainEvent handled , found {subscription.ChildTestDomainEventHandleCount}");

                Assert.IsOrBecomesTrue(
                    () => Interlocked.Read(ref subscription.ChildTestDomainEventHandleCount) == 1,
                    1000,
                    $"Expected 1 ChildTestDomainEvent handled , found {subscription.ChildTestDomainEventHandleCount}");

                Assert.IsOrBecomesTrue(
                    () => Interlocked.Read(ref subscription.ParentTestDomainEventHandleCount) == 1,
                    1000,
                    $"Expected 1 Parent Test Domain Event handled, found {subscription.ParentTestDomainEventHandleCount}");
               
            }
        }

        [Fact]
        public void parent_invokes_only_parent_handler_once()
        {
            using (var sub = new TestInheritedMessageSubscriber(Bus))
            {
                var subscriber = sub;
                TestQueue.Clear();

                Bus.Publish(new ParentTestDomainEvent(TestCorrelationId, ChildMsgId));

                BusMessages
                    .AssertNext<ParentTestDomainEvent>(TestCorrelationId)
                    .AssertEmpty();

                Assert.IsOrBecomesTrue(() => subscriber.Starving, 3000);

                Assert.IsOrBecomesTrue(
                    () => Interlocked.Read(ref subscriber.TestDomainEventHandleCount) == 0,
                    1000,
                    $"Expected 0 Test Domain Event Handled, found {subscriber.TestDomainEventHandleCount}");

                Assert.IsOrBecomesTrue(
                    () => Interlocked.Read(ref subscriber.GrandChildTestDomainEventHandleCount) == 0,
                    1000,
                    $"Expected 0 GrandChildTestDomainEvent handled , found {subscriber.ChildTestDomainEventHandleCount}");

                Assert.IsOrBecomesTrue(
                     () => Interlocked.Read(ref subscriber.ChildTestDomainEventHandleCount) == 0,
                    1000,
                    $"Expected 0 ChildTestDomainEvent handled , found {subscriber.ChildTestDomainEventHandleCount}");

                Assert.IsOrBecomesTrue(
                    () => Interlocked.Read(ref subscriber.ParentTestDomainEventHandleCount) == 1,
                    1000,
                    $"Expected 1 Parent Test Domain Event handled, found {subscriber.ParentTestDomainEventHandleCount}");
            }
        }
        [Fact]
        public void multiple_message_handle_invocations_are_correct()
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
                $"Expected 2 ChildTestDomainEvent handled , found {MessageSubscriber.ChildTestDomainEventHandleCount}");

            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref MessageSubscriber.ParentTestDomainEventHandleCount) == 3,
                1000,
                $"Expected 3 Parent Test Domain Event handled, found {MessageSubscriber.ParentTestDomainEventHandleCount}");


        }
    }
}
