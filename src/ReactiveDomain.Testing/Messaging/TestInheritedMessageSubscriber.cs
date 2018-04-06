using System.Threading;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Messaging.Testing
{
    public class TestInheritedMessageSubscriber :
        Messaging.Bus.QueuedSubscriber,
        IHandle<TestEvent>,
        IHandle<ParentTestEvent>,
        IHandle<ChildTestEvent>,
        IHandle<GrandChildTestEvent>,
        IHandle<Message>
    {
        public long TestDomainEventHandleCount;
        public long ParentTestDomainEventHandleCount;
        public long ChildTestDomainEventHandleCount;
        public long GrandChildTestDomainEventHandleCount;
        public long MessageHandleCount;

        public TestInheritedMessageSubscriber(IDispatcher bus, bool idempotent = true) : base(bus, idempotent)
        {
            Reset();

            Subscribe<TestEvent>(this);
            Subscribe<ParentTestEvent>(this);
            Subscribe<ChildTestEvent>(this);
            Subscribe<GrandChildTestEvent>(this);
        }

        public void Reset()
        {
            TestDomainEventHandleCount = 0;
            ParentTestDomainEventHandleCount = 0;
            ChildTestDomainEventHandleCount = 0;
            GrandChildTestDomainEventHandleCount = 0;
            MessageHandleCount = 0;
        }

        public void Handle(TestEvent message)
        {
            Interlocked.Increment(ref TestDomainEventHandleCount);
        }
        public void Handle(ParentTestEvent message)
        {
            Interlocked.Increment(ref ParentTestDomainEventHandleCount);
        }

        public void Handle(ChildTestEvent message)
        {
            Interlocked.Increment(ref ChildTestDomainEventHandleCount);
        }

        public void Handle(GrandChildTestEvent message)
        {
            Interlocked.Increment(ref GrandChildTestDomainEventHandleCount);
        }

        public void Handle(Message message)
        {
            Interlocked.Increment(ref MessageHandleCount);
        }
    }
}