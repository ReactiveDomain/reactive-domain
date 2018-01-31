using System.Threading;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Messaging.Testing;

namespace ReactiveDomain.Messaging.Tests.Subscribers.QueuedSubscriber
{
    public class TestQueuedSubscriber :
        Messaging.Bus.QueuedSubscriber,
        IHandle<TestMessage>,
        IHandle<TestMessage2>,
        IHandle<ChildTestMessage>
    {
        public long TimesChildTestMessageHandled;
        public long ParentTestMessage;
        public long TimesTestMessageHandled;
        public long TimesTestMessage2Handled;

        public TestQueuedSubscriber(IGeneralBus bus) : base(bus)
        {
            TimesTestMessageHandled = 0;
            TimesTestMessage2Handled = 0;
            TimesChildTestMessageHandled = 0;
            ParentTestMessage = 0;

            Subscribe<TestMessage>(this);
            Subscribe<TestMessage2>(this);
            Subscribe<ChildTestMessage>(this);
        }

        public void Handle(TestMessage message)
        {
            Interlocked.Increment(ref TimesTestMessageHandled);
        }

        public void Handle(TestMessage2 message)
        {            
            Interlocked.Increment(ref TimesTestMessage2Handled);
        }

        public void Handle(ChildTestMessage message)
        {
            Interlocked.Increment(ref TimesChildTestMessageHandled);
        }

    }
}
