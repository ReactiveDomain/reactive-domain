using System.Collections.Generic;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Messaging.Tests.Helpers;
using Xunit;

namespace ReactiveDomain.Messaging.Tests
{
// ReSharper disable once UnusedMember.Global
    public class when_using_testqueue
    {
        private readonly TestQueue _queue;
        private readonly IBus _bus;
        public when_using_testqueue()
        {
            _bus = new InMemoryBus("Test Bus");
            _queue = new TestQueue(_bus);
        }
        [Fact]
        public void can_get_all_messages_from_bus()
        {

            var msg1 = new TestMessage();
            var msg2 = new TestMessage();
            var msg3 = new TestMessage();
            var msgs = new Queue<Message>();
            msgs.Enqueue(msg1);
            msgs.Enqueue(msg2);
            msgs.Enqueue(msg3);

            _bus.Publish(msg1);
            _bus.Publish(msg2);
            _bus.Publish(msg3);

            Assert.Equal(3, _queue.Messages.Count);
            Assert.Contains(msg1, _queue.Messages);
            Assert.Contains(msg2, _queue.Messages);
            Assert.Contains(msg3, _queue.Messages);

            Assert.Equal(msgs,_queue.Messages);

        }

        [Fact]
        public void can_get_all_messages_from_enqueue()
        {
            var msg1 = new TestMessage();
            var msg2 = new TestMessage();
            var msg3 = new TestMessage();
            var msgs = new Queue<Message>();
            msgs.Enqueue(msg1);
            msgs.Enqueue(msg2);
            msgs.Enqueue(msg3);

            _queue.Handle(msg1);
            _queue.Handle(msg2);
            _queue.Handle(msg3);

            Assert.Equal(3, _queue.Messages.Count);
            Assert.Contains(msg1, _queue.Messages);
            Assert.Contains(msg2, _queue.Messages);
            Assert.Contains(msg3, _queue.Messages);

            Assert.Equal(msgs, _queue.Messages);
        }
        [Fact]
        public void can_clear_all_messages()
        {
            var msg1 = new TestMessage();
            var msg2 = new TestMessage();
            var msg3 = new TestMessage();
            var msgs = new Queue<Message>();
            msgs.Enqueue(msg1);
            msgs.Enqueue(msg2);
            msgs.Enqueue(msg3);

            _bus.Publish(msg1);
            _bus.Publish(msg2);
            _bus.Publish(msg3);

            _queue.Clear();
            Assert.Equal(0, _queue.Messages.Count);
            Assert.Empty(_queue.Messages);
        }

    }
}
