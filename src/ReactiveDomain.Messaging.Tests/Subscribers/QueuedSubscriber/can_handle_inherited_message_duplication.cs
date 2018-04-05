using System;
using System.Threading;
using ReactiveDomain.Messaging.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests.Subscribers.QueuedSubscriber
{
    public class can_handle_inherited_message_duplication : CommandBusSpecification
    {
        private CorrelationId _testCorrelationId;
        private TestEvent _testEvent;
        private ParentTestEvent _parentTestEvent;
        private ChildTestEvent _childTestEvent;

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
            _testCorrelationId = CorrelationId.NewId();
            _testEvent = new TestEvent(_testCorrelationId, SourceId.NullSourceId());
            Bus.Publish(_testEvent);

            _parentTestEvent = new ParentTestEvent(_testCorrelationId, new SourceId(_testEvent));
            Bus.Publish(_parentTestEvent);
            Assert.Equal(BusMessages.Count, 2);

            _childTestEvent = new ChildTestEvent(_testCorrelationId, new SourceId(_parentTestEvent));
            Bus.Publish(_childTestEvent);
            Assert.Equal(BusMessages.Count, 3);

            var msg4 = new GrandChildTestEvent(_testCorrelationId, new SourceId(_childTestEvent));
            Bus.Publish(msg4);
            Assert.Equal(BusMessages.Count, 4);
        }
        [Fact]
        public void grand_child_invokes_all_handlers_four_times()
        {
            //n.b. the number of duplications matched the number of times we have subscribed to the hierarchy
            //see the next test where we do not subscribe to Message
            // the subscription to Test Event does not matter because it tis outside the heirarchy
            using (var sub = new TestInheritedMessageSubscriber(Bus, false))
            {
                var subscriber = sub;
                subscriber.Subscribe<Message>(subscriber);

                TestQueue.Clear();
                Bus.Publish(new GrandChildTestEvent(_testCorrelationId, new SourceId(_childTestEvent)));

                BusMessages
                    .AssertNext<GrandChildTestEvent>(_testCorrelationId)
                    .AssertEmpty();

                Assert.IsOrBecomesTrue(() => subscriber.Starving, 3000);

                Assert.IsOrBecomesTrue(
                    () => Interlocked.Read(ref subscriber.TestDomainEventHandleCount) == 0,
                    1000,
                    $"Expected 0 Test Domain Event Handled, found {subscriber.TestDomainEventHandleCount}");

                Assert.IsOrBecomesTrue(
                    () => Interlocked.Read(ref subscriber.GrandChildTestDomainEventHandleCount) == 4,
                    3000,
                    $"Expected 4 GrandChildTestDomainEvent handled , found {subscriber.ChildTestDomainEventHandleCount}");

                Assert.IsOrBecomesTrue(
                    () => Interlocked.Read(ref subscriber.ChildTestDomainEventHandleCount) == 4,
                    3000,
                    $"Expected 4 ChildTestDomainEvent handled , found {subscriber.ChildTestDomainEventHandleCount}");

                Assert.IsOrBecomesTrue(
                    () => Interlocked.Read(ref subscriber.ParentTestDomainEventHandleCount) == 4,
                    3000,
                    $"Expected 4 Parent Test Domain Event handled, found {subscriber.ParentTestDomainEventHandleCount}");
                Assert.IsOrBecomesTrue(
                    () => Interlocked.Read(ref subscriber.MessageHandleCount) == 4,
                    3000,
                    $"Expected 4 Message Test Domain Event handled, found {subscriber.MessageHandleCount}");
            }
        }
        [Fact]
        public void grand_child_invokes_all_handlers_thrice()
        {
            using (var sub = new TestInheritedMessageSubscriber(Bus, false))
            {
                var subscriber = sub;
                TestQueue.Clear();
                Bus.Publish(new GrandChildTestEvent(_testCorrelationId, new SourceId(_childTestEvent)));

                BusMessages
                    .AssertNext<GrandChildTestEvent>(_testCorrelationId)
                    .AssertEmpty();

                Assert.IsOrBecomesTrue(
                    () => Interlocked.Read(ref subscriber.TestDomainEventHandleCount) == 0,
                    2000,
                    $"Expected 0 Test Domain Event Handled, found {subscriber.TestDomainEventHandleCount}");

                Assert.IsOrBecomesTrue(
                    () => Interlocked.Read(ref subscriber.GrandChildTestDomainEventHandleCount) == 3,
                    3000,
                    $"Expected 3 GrandChildTestDomainEvent handled , found {subscriber.ChildTestDomainEventHandleCount}");

                Assert.IsOrBecomesTrue(
                    () => Interlocked.Read(ref subscriber.ChildTestDomainEventHandleCount) == 3,
                    3000,
                    $"Expected 3 ChildTestDomainEvent handled , found {subscriber.ChildTestDomainEventHandleCount}");

                Assert.IsOrBecomesTrue(
                    () => Interlocked.Read(ref subscriber.ParentTestDomainEventHandleCount) == 3,
                    3000,
                    $"Expected 3 Parent Test Domain Event handled, found {subscriber.ParentTestDomainEventHandleCount}");
            }
        }
        [Fact]
        public void child_invokes_parent_and_child_handlers_twice()
        {
            using (var sub = new TestInheritedMessageSubscriber(Bus,false))
            {
                var subscriber = sub;
                TestQueue.Clear();
                Bus.Publish(new ChildTestEvent(_testCorrelationId, new SourceId(_childTestEvent)));

                BusMessages
                    .AssertNext<ChildTestEvent>(_testCorrelationId)
                    .AssertEmpty();

                Assert.IsOrBecomesTrue(
                    () => Interlocked.Read(ref subscriber.TestDomainEventHandleCount) == 0,
                    1000,
                    $"Expected 0 Test Domain Event Handled, found {subscriber.TestDomainEventHandleCount}");

                Assert.IsOrBecomesTrue(
                    () => Interlocked.Read(ref subscriber.GrandChildTestDomainEventHandleCount) == 0,
                    1000,
                    $"Expected 0 GrandChildTestDomainEvent handled , found {subscriber.ChildTestDomainEventHandleCount}");

                Assert.IsOrBecomesTrue(
                    () => Interlocked.Read(ref subscriber.ChildTestDomainEventHandleCount) == 2,
                    3000,
                    $"Expected 2 ChildTestDomainEvent handled , found {subscriber.ChildTestDomainEventHandleCount}");

                Assert.IsOrBecomesTrue(
                    () => Interlocked.Read(ref subscriber.ParentTestDomainEventHandleCount) == 2,
                    3000,
                    $"Expected 2 Parent Test Domain Event handled, found {subscriber.ParentTestDomainEventHandleCount}");
            }
        }
        [Fact]
        public void parent_invokes_only_parent_handler_once()
        {
            using (var sub = new TestInheritedMessageSubscriber(Bus))
            {
                var subscriber = sub;
                TestQueue.Clear();

                Bus.Publish(new ParentTestEvent(_testCorrelationId, new SourceId(_childTestEvent)));

                BusMessages
                    .AssertNext<ParentTestEvent>(_testCorrelationId)
                    .AssertEmpty();

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
        public void multiple_duplicate_message_handle_invocations_are_correct()
        {
           BusMessages
                .AssertNext<TestEvent>(_testCorrelationId)
                .AssertNext<ParentTestEvent>(_testCorrelationId)
                .AssertNext<ChildTestEvent>(_testCorrelationId)
                .AssertNext<GrandChildTestEvent>(_testCorrelationId)
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
