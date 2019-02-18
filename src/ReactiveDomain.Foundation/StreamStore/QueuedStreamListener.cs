using System;
using System.Threading;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Util;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation
{
    public class QueuedStreamListener : StreamListener, IHandle<Message>
    {
        protected readonly QueuedHandler SyncQueue;
        private long _startWait;
        private long _pending;
        private ManualResetEventSlim _running = new ManualResetEventSlim(true);

        public QueuedStreamListener(
            string name,
            IStreamStoreConnection connection,
            IStreamNameBuilder streamNameBuilder,
            IEventSerializer serializer,
            string busName = null) :
                base(name, connection, streamNameBuilder, serializer, busName)
        {
            SyncQueue = new QueuedHandler(this, "SyncListenerQueue");
        }

        protected override void GotEvent(RecordedEvent recordedEvent)
        {
            if (_disposed) return; //todo: fix dispose
            Interlocked.Exchange(ref StreamPosition, recordedEvent.EventNumber);
            if (Serializer.Deserialize(recordedEvent) is Message @event)
            {
                //todo: this needs to publish a RecordedEvent
                SyncQueue.Publish(@event);
            }
        }
        public void Handle(Message @event)
        {
            _running.Wait();
            //todo: this needs to take a RecordedEvent
            Bus.Publish(@event);
            if (Interlocked.Read(ref _startWait) == 1)
            {
                Interlocked.Decrement(ref _pending);
            }
        }

        public override void Start(string streamName, long? checkpoint = null, bool waitUntilLive = false, int millisecondsTimeout = 1000)
        {
            SyncQueue?.Start();
            base.Start(streamName, checkpoint, waitUntilLive, millisecondsTimeout);
            if (waitUntilLive)
            {
                Interlocked.Exchange(ref _pending, SyncQueue.MessageCount);
                Interlocked.Exchange(ref _startWait, 1);
                SpinWait.SpinUntil(() => Interlocked.Read(ref _pending) < 1 || SyncQueue.Idle, millisecondsTimeout);
                Interlocked.Exchange(ref _startWait, 0);
            }
        }

        IDisposable Pause() {
            _running.Reset();
            return new Disposer(() => { Resume(); return Unit.Default; });
        }
        void Resume() {
            _running.Set();
        }
        private bool _disposed;
        protected override void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
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