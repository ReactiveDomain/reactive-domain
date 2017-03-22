using System;
using System.Threading;
using ReactiveDomain.Messaging;
using ReactiveDomain.Tests.Helpers;
using Xunit;

namespace ReactiveDomain.Tests
{
    // ReSharper disable InconsistentNaming
    public class can_process_queued_messages : when_using_queued_subscriber
    {
        protected Guid TestMessageId;
        protected Guid TestCorrelationId;

        protected override void When()
        {

            var msg = new TestMessage();
            TestMessageId = msg.MsgId;

            Bus.Publish(msg);
        }

        [Fact]
        public void can_handle_message() 
        {
            Assert.IsOrBecomesTrue(
                () => BusMessages.Count == 1,
                null,
                $"Expected 1 Message, found {BusMessages.Count}");

            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref _messageSubscriber.TimesTestMessageHandled) == 1, 
                1000,
                $"Expected 1 Message, found {_messageSubscriber.TimesTestMessageHandled}");
        }

        [Fact]
        public void can_handle_two_messages() 
        {
            var msg2 = new TestMessage();
            Bus.Publish(msg2);
            Assert.IsOrBecomesTrue(
                () => BusMessages.Count == 2,
                1000,
                $"Expected 2 Messages on bus, found {BusMessages.Count}");

            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref _messageSubscriber.TimesTestMessageHandled) == 2,
                1000,
                $"Expected 2 Messages handled, found {_messageSubscriber.TimesTestMessageHandled}");
        }

        [Fact]
        public void can_handle_two_different_messages() 
        {
            var msg2 = new TestMessage2();
            Bus.Publish(msg2);
            Assert.IsOrBecomesTrue(
                () => BusMessages.Count == 2,
                null,
                $"Expected 2 Messages on bus, found {BusMessages.Count}");

            Message deQdMsg;
            if (BusMessages.TryDequeue(out deQdMsg))
            {
                Assert.IsType<TestMessage>(deQdMsg);
            }
            if (BusMessages.TryDequeue(out deQdMsg))
            {
                Assert.IsType<TestMessage2>(deQdMsg);
            }

            Assert.False(BusMessages.TryDequeue(out deQdMsg));

            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref _messageSubscriber.TimesTestMessageHandled) == 1,
                1000,
                $"Expected 1 TestMessage, found {_messageSubscriber.TimesTestMessageHandled}");
            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref _messageSubscriber.TimesTestMessage2Handled) == 1,
                100,
                $"Expected 1 TestMessage2, found {_messageSubscriber.TimesTestMessage2Handled}");
        }

        [Fact]
        public void cannot_create_queued_subscriber_without_bus() 
        {
            var ex =
                 Assert.Throws<ArgumentNullException>(
                                                        () => new TestMessageSubscriber(null)
                                                    );
        }

        [Fact]
        public void can_handle_child_messages()
        {

            var msg3 = new ChildTestMessage();
            Bus.Publish(msg3);
            Assert.Equal(BusMessages.Count, 2);

            Message deQdMsg;
            if (BusMessages.TryDequeue(out deQdMsg))
            {
                Assert.IsType<TestMessage>(deQdMsg);
            }
            if (BusMessages.TryDequeue(out deQdMsg))
            {
                Assert.IsType<ChildTestMessage>(deQdMsg);
            }

            Assert.False(BusMessages.TryDequeue(out deQdMsg));

            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref _messageSubscriber.TimesTestMessageHandled) == 1,
                1000,
                $"Expected 1 TestMessage, found {_messageSubscriber.TimesTestMessageHandled}");


            Assert.IsOrBecomesTrue(
            () => Interlocked.Read(ref _messageSubscriber.TimesChildTestMessageHandled) == 1,
                null,
                $"Expected 1 ChildTestMessage, found {_messageSubscriber.TimesChildTestMessageHandled}");

        }

        [Fact]
        public void can_handle_multiple_subscribers()
        {
            TestMessageSubscriber secondSubscriber = new TestMessageSubscriber(Bus);

            var msg2 = new TestMessage2();
            Bus.Publish(msg2);
            Assert.Equal(BusMessages.Count, 2);

            var msg3 = new TestMessage2();
            Bus.Publish(msg3);
            Assert.Equal(BusMessages.Count, 3);

            Message deQdMsg;
            if (BusMessages.TryDequeue(out deQdMsg))
            {
                Assert.IsType<TestMessage>(deQdMsg);
            }
            if (BusMessages.TryDequeue(out deQdMsg))
            {
                Assert.IsType<TestMessage2>(deQdMsg);
            }

            if (BusMessages.TryDequeue(out deQdMsg))
            {
                Assert.IsType<TestMessage2>(deQdMsg);
            }

            Assert.False(BusMessages.TryDequeue(out deQdMsg));

            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref _messageSubscriber.TimesTestMessageHandled) == 1,
                1000,
                $"Expected 1 TestMessage, handled by first subscriber {_messageSubscriber.TimesTestMessageHandled}");

            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref secondSubscriber.TimesTestMessageHandled) == 0,
                1000,
                $"Expected 0 TestMessage handled by second subscriber, found {secondSubscriber.TimesTestMessageHandled}");

            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref _messageSubscriber.TimesTestMessage2Handled) == 2,
                1000,
                $"Expected 2 TimesTestMessage2Handled by secondSubscriber, found {_messageSubscriber.TimesTestMessage2Handled}");

            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref secondSubscriber.TimesTestMessage2Handled) == 2,
                1000,
                $"Expected 2 TimesTestMessage2Handled by second subscriber, found {secondSubscriber.TimesTestMessage2Handled}");
        }

        [Fact]
        public void can_handle_inherited_messages()
        {
            TestInheritedMessageSubscriber secondSubscriber = new TestInheritedMessageSubscriber(Bus);

            var msg2 = new ParentTestMessage();
            Bus.Publish(msg2);
            Assert.Equal(BusMessages.Count, 2);

            var msg3 = new ChildTestMessage();
            Bus.Publish(msg3);
            Assert.Equal(BusMessages.Count, 3);

            var msg4 = new GrandChildTestMessage();
            Bus.Publish(msg4);
            Assert.Equal(BusMessages.Count, 4);

            Message deQdMsg;
            if (BusMessages.TryDequeue(out deQdMsg))
            {
                Assert.IsType<TestMessage>(deQdMsg);
            }
            if (BusMessages.TryDequeue(out deQdMsg))
            {
                Assert.IsType<ParentTestMessage>(deQdMsg);
            }

            if (BusMessages.TryDequeue(out deQdMsg))
            {
                Assert.IsType<ChildTestMessage>(deQdMsg);
            }

            if (BusMessages.TryDequeue(out deQdMsg))
            {
                Assert.IsType<GrandChildTestMessage>(deQdMsg);
            }

            Assert.False(BusMessages.TryDequeue(out deQdMsg));

            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref _messageSubscriber.TimesTestMessageHandled) == 1,
                1000,
                $"Expected 1 TestMessage, handled by first subscriber {_messageSubscriber.TimesTestMessageHandled}");

            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref secondSubscriber.TimesParentTestMessageHandled) == 1,
                1000,
                $"Expected 1 TimesParentTestMessageHandled by second subscriber, found {secondSubscriber.TimesParentTestMessageHandled}");

            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref secondSubscriber.TimesChildTestMessageHandled) == 1,
                1000,
                $"Expected 1 TimesChildTestMessageHandled by second subscriber, found {secondSubscriber.TimesChildTestMessageHandled}");

            Assert.IsOrBecomesTrue(
                () => Interlocked.Read(ref secondSubscriber.TimesGrandChildTestMessageHandled) == 1,
                1000,
                $"Expected 1 TimesGrandChildTestMessageHandled by second subscriber, found {secondSubscriber.TimesGrandChildTestMessageHandled}");

        }
    }
}