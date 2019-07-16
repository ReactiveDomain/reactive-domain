using System;
using System.Threading;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests.Subscribers.QueuedSubscriber {
    public class QueuedOrderingFixture {
        public Dispatcher Dispatcher { get; }

        public long EventCount;
        public long CmdCount;
        public long MsgCount;
        public bool IsInOrder = true;
        private TestSubscriber _subscriber;

        public QueuedOrderingFixture() {
            Dispatcher = new Dispatcher("Test");
            _subscriber = new TestSubscriber(Dispatcher, this);
        }

        public void Reset() {
            EventCount = 0;
            CmdCount = 0;
            MsgCount = 0;
            IsInOrder = true;
        }
        private class TestSubscriber :
            Bus.QueuedSubscriber,
            IHandle<CountedEvent>,
            IHandle<CountedTestMessage> {
            private readonly QueuedOrderingFixture _fixture;
            public TestSubscriber(IBus bus, QueuedOrderingFixture fixture) : base(bus) {
                _fixture = fixture;
                Subscribe<CountedEvent>(this);
                Subscribe<CountedTestMessage>(this);
                Subscribe(new AdHocCommandHandler<TestCommands.OrderedCommand>(
                    cmd => {
                        Interlocked.Increment(ref _fixture.CmdCount);
                        return Interlocked.Read(ref _fixture.CmdCount) == cmd.SequenceNumber + 1;
                    }));
            }
            void IHandle<CountedEvent>.Handle(CountedEvent @event) {
                if (@event.MessageNumber != _fixture.EventCount)
                    _fixture.IsInOrder = false;
                Interlocked.Increment(ref _fixture.EventCount);
            }

            void IHandle<CountedTestMessage>.Handle(CountedTestMessage message) {
                if (message.MessageNumber != _fixture.MsgCount)
                    _fixture.IsInOrder = false;
                Interlocked.Increment(ref _fixture.MsgCount);
            }
        }

    }
    // ReSharper disable once InconsistentNaming
    // ReSharper disable once RedundantExtendsListEntry
    public sealed class when_using_queued_subscriber_message_order_is_preserved :
        IClassFixture<QueuedOrderingFixture> {
        private readonly QueuedOrderingFixture _fixture;
        private readonly int _count = 100;

        public when_using_queued_subscriber_message_order_is_preserved(QueuedOrderingFixture fixture) {
            _fixture = fixture;
        }

        [Fact]
        void can_handle_messages_in_order() {
            _fixture.Reset();
            for (int i = 0; i < _count; i++) {
                _fixture.Dispatcher.Publish(new CountedTestMessage(i));
            }
            AssertEx.IsOrBecomesTrue(
                () => Interlocked.Read(ref _fixture.MsgCount) == _count,
                msg: $"Expected message count to be {_count} Messages, found {_fixture.MsgCount}");
            Assert.True(_fixture.IsInOrder);
        }

        [Fact]
        void can_handle_events_in_order() {
            _fixture.Reset();           
            for (int i = 0; i < _count; i++) {
                var evt = MessageBuilder.New(()=> new CountedEvent(i));
                _fixture.Dispatcher.Publish(evt);               
            }
            AssertEx.IsOrBecomesTrue(
                () => Interlocked.Read(ref _fixture.EventCount) == _count,
                msg: $"Expected message count to be {_count} Messages, found {_fixture.EventCount}");
            Assert.True(_fixture.IsInOrder);
        }
        [Fact]
        void can_handle_commands_in_order() {
            _fixture.Reset();
           
            for (int i = 0; i < _count; i++) {
                var cmd = new TestCommands.OrderedCommand(i);
                _fixture.Dispatcher.Send(cmd);
            }
            AssertEx.IsOrBecomesTrue(
                () => Interlocked.Read(ref _fixture.CmdCount) == _count,
                msg: $"Expected message count to be {_count} Messages, found {_fixture.CmdCount}");
        }

    }
}
