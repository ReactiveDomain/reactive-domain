using System;
using ReactiveDomain.Messaging.Testing;

namespace ReactiveDomain.Messaging.Tests.Subscribers.QueuedSubscriber {
    // ReSharper disable InconsistentNaming
    public abstract class when_using_inherited_queued_subscriber : IDisposable {
        protected TestInheritedMessageSubscriber MessageSubscriber;
        protected Bus.IDispatcher Bus;
        public when_using_inherited_queued_subscriber() {

            Bus = new Bus.Dispatcher(nameof(when_using_queued_subscriber));
            MessageSubscriber = new TestInheritedMessageSubscriber(Bus);
        }
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing) {
            if (disposing) {
                MessageSubscriber?.Dispose();
                Bus?.Dispose();
            }
        }
    }
}
