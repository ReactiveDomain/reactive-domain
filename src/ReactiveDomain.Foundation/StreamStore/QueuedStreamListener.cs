using System;
using System.Threading;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Util;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation
{
    public class QueuedStreamListener : StreamListener, IHandle<IMessage>
    {
        protected readonly QueuedHandler SyncQueue;
        private ManualResetEventSlim _isLive = new ManualResetEventSlim(false);
        private long _pendingCount;
        private ManualResetEventSlim _running = new ManualResetEventSlim(true);

        public QueuedStreamListener(
            string name,
            IStreamStoreConnection connection,
            IStreamNameBuilder streamNameBuilder,
            IEventSerializer serializer,
            string busName = null,
            Action<Unit> liveProcessingStarted = null,
            Action<SubscriptionDropReason, Exception> subscriptionDropped = null) :
                base(name, connection, streamNameBuilder, serializer, busName, liveProcessingStarted, subscriptionDropped)
        {
            SyncQueue = new QueuedHandler(this, "SyncListenerQueue");
        }

        protected override void GotEvent(RecordedEvent recordedEvent)
        {
            if (_disposed) return; //todo: fix dispose
            Interlocked.Exchange(ref StreamPosition, recordedEvent.EventNumber);
            if (Serializer.Deserialize(recordedEvent) is IMessage @event)
            {
                //todo: this needs to publish a RecordedEvent
                SyncQueue.Publish(@event);
            }
        }
        public void Handle(IMessage @event)
        {
            _running.Wait();
            //todo: this needs to take a RecordedEvent
            Bus.Publish(@event);

            if (!_isLive.IsSet)
            {
                Interlocked.Decrement(ref _pendingCount);
                if (base.IsLive && (Interlocked.Read(ref _pendingCount) <= 0 || SyncQueue.Idle))
                {
                    _isLive.Set();
                }
            }
        }

        public override void Start(string streamName, long? checkpoint = null, bool waitUntilLive = false, CancellationToken cancelWaitToken = default(CancellationToken))
        {
            _isLive.Reset();

            SyncQueue?.Start();
            base.Start(streamName, checkpoint, waitUntilLive, cancelWaitToken);

            Interlocked.Exchange(ref _pendingCount, SyncQueue.MessageCount);
            if (Interlocked.Read(ref _pendingCount) <= 0 || SyncQueue.Idle)
            {
                _isLive.Set();
            }

            if (waitUntilLive)
            {
                _isLive.Wait(cancelWaitToken);
            }
        }

        IDisposable Pause()
        {
            _running.Reset();
            return new Disposer(() => { Resume(); return Unit.Default; });
        }
        void Resume()
        {
            _running.Set();
        }
        private bool _disposed;
        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _isLive.Set();
                    _running.Reset();
                    SyncQueue?.Stop();
                    _running.Dispose();
                }
                _disposed = true;
            }
            base.Dispose(disposing);
        }
    }
}