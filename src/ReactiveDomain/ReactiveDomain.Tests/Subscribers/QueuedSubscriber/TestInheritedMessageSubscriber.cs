using System.Threading;
using ReactiveDomain.Bus;
using ReactiveDomain.Tests.Helpers;

namespace ReactiveDomain.Tests.Subscribers.QueuedSubscriber
{
    public class TestInheritedMessageSubscriber :
        Bus.QueuedSubscriber,
        IHandle<TestDomainEvent>,
        IHandle<ParentTestDomainEvent>,
        IHandle<ChildTestDomainEvent>,
        IHandle<GrandChildTestDomainEvent>
    {
        public long TestDomainEventHandleCount;
        public long ParentTestDomainEventHandleCount;
        public long ChildTestDomainEventHandleCount;
        public long GrandChildTestDomainEventHandleCount;

        public TestInheritedMessageSubscriber(IGeneralBus bus, bool idenpotent = true) : base(bus, idenpotent)
        {
            Reset();

            Subscribe<TestDomainEvent>(this);
            Subscribe<ParentTestDomainEvent>(this);
            Subscribe<ChildTestDomainEvent>(this);
            Subscribe<GrandChildTestDomainEvent>(this);
        }

        public void Reset()
        {
            TestDomainEventHandleCount = 0;
            ParentTestDomainEventHandleCount = 0;
            ChildTestDomainEventHandleCount = 0;
            GrandChildTestDomainEventHandleCount = 0;
        }

        public void Handle(TestDomainEvent message)
        {
            Interlocked.Increment(ref TestDomainEventHandleCount);
        }
        public void Handle(ParentTestDomainEvent message)
        {
            Interlocked.Increment(ref ParentTestDomainEventHandleCount);
        }

        public void Handle(ChildTestDomainEvent message)
        {
            Interlocked.Increment(ref ChildTestDomainEventHandleCount);
        }

        public void Handle(GrandChildTestDomainEvent message)
        {
            Interlocked.Increment(ref GrandChildTestDomainEventHandleCount);
        }
    }
}