using System.Collections.Generic;
using System.Threading;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests.Subscribers.QueuedSubscriber {
    // ReSharper disable once InconsistentNaming
    public sealed class can_unsubscribe_queued_messages {

        private readonly IDispatcher _bus = new Dispatcher("test", 3);
        private int _msgCount = 20;
        private readonly List<Message> _messages = new List<Message>();
        private int _messageCount;
        private int _eventCount;

        private class TestSubscriber : Bus.QueuedSubscriber {
            public TestSubscriber(IBus bus) : base(bus) { }
        }

        public can_unsubscribe_queued_messages() {
            var source = CorrelatedMessage.NewRoot();
            for (var i = 0; i < _msgCount; i++) {
                _messages.Add(new CountedTestMessage(i));
                var evt = new CountedEvent(i, source);
                _messages.Add(evt);
                source = evt;
            }
        }
        [Fact]
        void can_unsubscribe_messages_and_events_by_disposing() {
            using (var sub = new TestSubscriber(_bus)) {
                sub.Subscribe(new AdHocHandler<CountedTestMessage>(_ => Interlocked.Increment(ref _messageCount)));
                sub.Subscribe(new AdHocHandler<CountedEvent>(_ => Interlocked.Increment(ref _eventCount)));

                foreach (var msg in _messages) {
                    _bus.Publish(msg);
                }
                AssertEx.IsOrBecomesTrue(() => _messageCount == _msgCount);
                AssertEx.IsOrBecomesTrue(() => _eventCount == _msgCount);

            }
            
            for (int i = 0; i < _msgCount; i++) {
                _bus.Publish(_messages[i]);
            }

            Assert.True(_messageCount == _msgCount);
            Assert.True(_eventCount == _msgCount);
        }
    }
}
