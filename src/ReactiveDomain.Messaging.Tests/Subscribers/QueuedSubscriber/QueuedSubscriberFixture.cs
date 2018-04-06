using System;
using System.Threading;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Messaging.Testing;

namespace ReactiveDomain.Messaging.Tests.Subscribers.QueuedSubscriber {
    public sealed class QueuedSubscriberFixture : IPublisher {
        private readonly Subscriber _subscriber;
        private readonly IBus _bus;
        public QueuedSubscriberFixture() {
            _bus = new InMemoryBus("test");
            _subscriber = new Subscriber(_bus);
            Warmup();
        }

        private void Warmup() {
            for (int i = 0; i < 10; i++) {
                _bus.Publish(new TestEvent(CorrelationId.NewId(), SourceId.NullSourceId()));
            }
            SpinWait.SpinUntil(() => _subscriber.Starving);
            Clear();
        }

        public long TestEventCount => _subscriber.TestEventCount;
        public long ParentEventCount => _subscriber.ParentEventCount;
        public long ChildEventCount => _subscriber.ChildEventCount;
        public long GrandChildEventCount => _subscriber.GrandChildEventCount;

        public bool Idle => _subscriber.Starving;

        public void Clear() {
            Interlocked.Exchange(ref _subscriber.TestEventCount, 0);
            Interlocked.Exchange(ref _subscriber.ParentEventCount, 0);
            Interlocked.Exchange(ref _subscriber.ChildEventCount, 0);
            Interlocked.Exchange(ref _subscriber.GrandChildEventCount, 0);
        }
        public void Publish(Message message) => _bus.Publish(message);

        private class Subscriber :
            Bus.QueuedSubscriber,
            IHandle<TestEvent>,
            IHandle<ParentTestEvent>,
            IHandle<ChildTestEvent>,
            IHandle<GrandChildTestEvent> {
            private bool _disposed;
            public long TestEventCount;
            public long ParentEventCount;
            public long ChildEventCount;
            public long GrandChildEventCount;
            public Subscriber(IBus bus) : base(bus) {

                Subscribe<TestEvent>(this);
                Subscribe<ParentTestEvent>(this);
                Subscribe<ChildTestEvent>(this);
                Subscribe<GrandChildTestEvent>(this);
            }

            public void Handle(TestEvent message) => Interlocked.Increment(ref TestEventCount);

            public void Handle(ParentTestEvent message) => Interlocked.Increment(ref ParentEventCount);

            public void Handle(ChildTestEvent message) => Interlocked.Increment(ref ChildEventCount);

            public void Handle(GrandChildTestEvent message) => Interlocked.Increment(ref GrandChildEventCount);

           
        }
    }
}