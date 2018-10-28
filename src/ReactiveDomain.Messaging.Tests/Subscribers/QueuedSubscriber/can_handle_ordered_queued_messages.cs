using System;
using System.Threading;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests.Subscribers.QueuedSubscriber {
    // ReSharper disable once InconsistentNaming
    // ReSharper disable once RedundantExtendsListEntry
    public sealed class can_handle_ordered_queued_messages : 
        IHandle<CountedEvent>,
        IHandle<CountedTestMessage>, IDisposable
    {
        private readonly int _count = 100;
        private readonly IDispatcher _bus;
        private readonly Bus.QueuedSubscriber _subscriber;
        private long _eventCount;
        private long _cmdCount;
        private long _testMsgCount;

        private bool _isInOrder = true;
        

        private class TestSubscriber : Bus.QueuedSubscriber
        {
            public TestSubscriber(IBus bus) : base(bus) { }
        }

        public can_handle_ordered_queued_messages() {
            _bus = new Dispatcher("test");
            _subscriber = new TestSubscriber(_bus);
            _subscriber.Subscribe<CountedEvent>(this);
            _subscriber.Subscribe<CountedTestMessage>(this);
        }

        [Fact]
        void can_handle_messages_in_order() {
            for (int i = 0; i < _count; i++) {
                _bus.Publish(new CountedTestMessage(i));
            }
            AssertEx.IsOrBecomesTrue(
                () => _testMsgCount == _count,
                msg: $"Expected message count to be {_count} Messages, found {_testMsgCount}");
            Assert.True(_isInOrder);
        }

        [Fact] 
        void can_handle_events_in_order() {
            CorrelatedMessage source = CorrelatedMessage.NewRoot();
            for (int i = 0; i < _count; i++) {
                var evt = new CountedEvent(i, source);
                _bus.Publish(evt);
                source = evt;
            }
            AssertEx.IsOrBecomesTrue(
                () => _eventCount == _count,
                msg: $"Expected message count to be {_count} Messages, found {_eventCount}");
            Assert.True(_isInOrder);
        }
        [Fact] 
        void can_handle_cmds_in_order() {
            _subscriber.Subscribe(new AdHocCommandHandler<TestCommands.OrderedCommand>(
                cmd => {
                    Interlocked.Increment(ref _cmdCount);
                    return Interlocked.Read(ref _cmdCount) == cmd.SequenceNumber + 1;
                }));
            CorrelatedMessage source = CorrelatedMessage.NewRoot();
            for (int i = 0; i < _count; i++) {
                var cmd = new TestCommands.OrderedCommand(i, source);
                _bus.Send(cmd);
                source = cmd;
            }
            AssertEx.IsOrBecomesTrue(
                () => Interlocked.Read(ref _cmdCount) == _count,
                msg: $"Expected message count to be {_count} Messages, found {_cmdCount}");
        }

        void IHandle<CountedEvent>.Handle(CountedEvent message) {
            if (message.MessageNumber != _eventCount)
                _isInOrder = false;
            Interlocked.Increment(ref _eventCount);
        }

        void IHandle<CountedTestMessage>.Handle(CountedTestMessage message) {
            if (message.MessageNumber != _testMsgCount)
                _isInOrder = false;
            Interlocked.Increment(ref _testMsgCount);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing) {
            if (!disposing) return;
          
            _subscriber?.Dispose();
        }
    }
}
