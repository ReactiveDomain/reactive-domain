using System.Threading;
using EventStore.ClientAPI;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Foundation.EventStore
{
    public class SynchronizableStreamListener : StreamListener, IHandle<Message>
    {

        protected bool Sync;
        protected readonly QueuedHandler SyncQueue;

        public SynchronizableStreamListener(
            string name,
            IEventStoreConnection connection,
            IStreamNameBuilder streamNameBuilder,
            bool sync = false,
            string busName = null) :
                base(name, connection, streamNameBuilder, busName)
        {

            Sync = sync;
            SyncQueue = new QueuedHandler(this, "SyncListenerQueue");
        }

        protected override void GotEvent(Message @event)
        {
            if (_disposed) return; //todo: fix dispose
            if (Sync)
                SyncQueue.Publish(@event);
            else
                base.GotEvent(@event);

        }
        public void Handle(Message @event)
        {
            base.GotEvent(@event);
        }

        public override void Start(string streamName, int? checkpoint = null, bool waitUntilLive = false, int millisecondsTimeout = 1000)
        {
            if (Sync)
                SyncQueue.Start();
            base.Start(streamName, checkpoint, waitUntilLive, millisecondsTimeout);
            if (waitUntilLive)
                SpinWait.SpinUntil(() => SyncQueue.Idle, millisecondsTimeout);
        }

        private bool _disposed;
        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    SyncQueue?.Stop();
                }
                _disposed = true;
            }
            base.Dispose(disposing);
        }
    }
}