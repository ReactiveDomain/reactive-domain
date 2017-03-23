using System.Threading;
using ReactiveDomain.Bus;

namespace ReactiveDomain.Tests.Helpers
{
    public class TestMessageSubscriber :
        QueuedSubscriber,
        IHandle<TestMessage>,
        IHandle<TestMessage2>,
        IHandle<ChildTestMessage>,
        IHandle<ParentTestMessage>
    {
        public long TimesChildTestMessageHandled;
        public long ParentTestMessage;
        public long TimesTestMessageHandled;
        public long TimesTestMessage2Handled;
        public override void HandleDynamic(dynamic message)
        {
            Handle(message);
        }

        public TestMessageSubscriber(IGeneralBus bus) : base(bus)
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

        public void Handle(ParentTestMessage message)
        {
            Interlocked.Increment(ref ParentTestMessage);
        }
    }

    public class TestInheritedMessageSubscriber :
        QueuedSubscriber,
        IHandle<GrandChildTestMessage>,
        IHandle<ChildTestMessage>,
        IHandle<ParentTestMessage>
    {
        public long TimesChildTestMessageHandled;
        public long TimesParentTestMessageHandled;
        public long TimesGrandChildTestMessageHandled;
        public override void HandleDynamic(dynamic message)
        {
            Handle(message);
        }

        public TestInheritedMessageSubscriber(IGeneralBus bus) : base(bus)
        {
            TimesGrandChildTestMessageHandled = 0;
            TimesChildTestMessageHandled = 0;
            TimesParentTestMessageHandled = 0;

            Subscribe<GrandChildTestMessage>(this);
            Subscribe<ChildTestMessage>(this);
            Subscribe<ParentTestMessage>(this);
        }

        public void Handle(ChildTestMessage message)
        {
            Interlocked.Increment(ref TimesChildTestMessageHandled);
        }

        public void Handle(ParentTestMessage message)
        {
            Interlocked.Increment(ref TimesParentTestMessageHandled);
        }

        public void Handle(GrandChildTestMessage message)
        {
            Interlocked.Increment(ref TimesGrandChildTestMessageHandled);
        }
    }
}
