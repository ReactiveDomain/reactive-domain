using System;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Messaging.Util;

namespace ReactiveDomain.Foundation.Tests.EventStore
{
    public class MockSubscriber : ISubscriber
    {
        private IBus _bus;

        public MockSubscriber(IBus bus)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        }

        public bool HasSubscriberFor<T>(bool includeDerived = false) where T : Message
        {
            return _bus.HasSubscriberFor<T>(includeDerived);
        }

        public IDisposable Subscribe<T>(IHandle<T> handler) where T : Message
        {
            _bus.Subscribe(handler);
            return new SubscriptionDisposer(() => { this.Unsubscribe(handler); return Unit.Default; });
        }

        public void Unsubscribe<T>(IHandle<T> handler) where T : Message
        {
            _bus.Unsubscribe(handler);
        }
    }
}
