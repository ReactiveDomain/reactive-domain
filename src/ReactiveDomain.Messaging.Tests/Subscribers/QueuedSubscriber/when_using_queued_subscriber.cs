using System;
using System.Threading;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Messaging.Testing;

namespace ReactiveDomain.Messaging.Tests.Subscribers.QueuedSubscriber {
    // ReSharper disable InconsistentNaming
    public abstract class when_using_queued_subscriber :IHandle<Message>, IDisposable {
        protected TestQueuedSubscriber MessageSubscriber;
        protected Bus.IDispatcher Bus;
        private long _msgCount;
        public long MsgCount => _msgCount;
        public when_using_queued_subscriber() {
            Bus = new Dispatcher(nameof(when_using_queued_subscriber));
            MessageSubscriber = new TestQueuedSubscriber(Bus);
            Bus.Subscribe(this);
        }
        public void Handle(Message message) {
            Interlocked.Increment(ref _msgCount);
        }
        public void Clear() {
            Interlocked.Exchange(ref _msgCount, 0);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) {
            if (!disposing) return;
            MessageSubscriber?.Dispose();
            Bus?.Dispose();
        }
    }
}
