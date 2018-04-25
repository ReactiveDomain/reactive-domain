using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests.Subscribers.QueuedSubscriber
{
    // ReSharper disable once InconsistentNaming
    public sealed class can_unsubscribe_queued_messages : 
                            IDisposable
    {
        private readonly CountedMessageSubscriber _sub;
        private readonly IDispatcher _bus = new Dispatcher("test", 3, false);
        private int _msgCount = 20;
        private readonly List<Message> _messages = new List<Message>();

        public can_unsubscribe_queued_messages()
        {
            CorrelatedMessage source = CorrelatedMessage.NewRoot();
            _sub = new CountedMessageSubscriber(_bus);
            for (var i = 0; i < _msgCount; i++)
            {
                _messages.Add(new CountedTestMessage(i));
                var evt = new CountedEvent(i, source);
                _messages.Add(evt);
                source = evt;
            }
        }
        [Fact]
        void can_unsubscribe_messages_and_events_by_disposing()
        {
            Parallel.ForEach(_messages, msg => _bus.Publish(msg));
            var sub = _sub;
            Assert.IsOrBecomesTrue(() => sub.MessagesHandled == _msgCount);
            Assert.IsOrBecomesTrue(() => sub.EventsHandled == _msgCount);

            _sub.Dispose();

            Parallel.ForEach(_messages, msg => _bus.Publish(msg));

            Assert.True(_sub.MessagesHandled == _msgCount);
            Assert.True(_sub.EventsHandled == _msgCount);
        }


        public void Dispose()
        {
            _bus?.Dispose();
        }
    }
}
