using System;
using System.Threading;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Util;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation
{
    /// <summary>
    /// StreamListener
    /// This class wraps a StreamStoreSubscription and is primarily used in the building of read models. 
    /// The Raw events returned from the Stream will be unwrapped using the provided serializer and
    /// consumers can subscribe to event notifications via the exposed EventStream.
    ///</summary>
    /// <remarks>
    /// N.B. The callbacks on the EventStream subscriptions will use the thread pool threads from the
    /// Subscription and are not guaranteed to complete in order, especially if handlers require variable
    /// amounts of time to complete processing. This can cause out of order events to be seen in the readmodel.
    /// If event ordering is required use the QueuedListener or a QueuedHandler to dequeue the events in order.
    /// </remarks> 
    public class StreamListener : IListener
    {
        protected readonly string ListenerName;
        protected readonly InMemoryBus Bus;
        IDisposable _subscription;
        private bool _started;
        private readonly IStreamNameBuilder _streamNameBuilder;
        protected readonly IEventSerializer Serializer;
        private readonly Action<Unit> _liveProcessingStarted;
        private readonly Action<SubscriptionDropReason, Exception> _subscriptionDropped;
        private readonly object _startLock = new object();
        private readonly ManualResetEventSlim _liveLock = new ManualResetEventSlim();
        public bool IsLive => _liveLock.IsSet;
        public ISubscriber EventStream => Bus;
        private readonly IStreamStoreConnection _streamStoreConnection;
        protected long StreamPosition;
        public long Position => StreamPosition;
        public string StreamName { get; private set; }
        public CatchUpSubscriptionSettings Settings { get; set; }

        /// <summary>
        /// For listening to generic streams 
        /// </summary>
        /// <param name="listenerName"></param>
        /// <param name="streamStoreConnection">The event store to subscribe to</param>
        /// <param name="streamNameBuilder">The source for correct stream names based on aggregates and events</param>
        /// <param name="serializer"></param>
        /// <param name="busName">The name to use for the internal bus (helpful in debugging)</param>
        /// <param name="liveProcessingStarted">An Action to invoke when live processing starts</param>
        /// <param name="subscriptionDropped">An Action to invoke if a subscription is dropped</param>
        public StreamListener(
                string listenerName,
                IStreamStoreConnection streamStoreConnection,
                IStreamNameBuilder streamNameBuilder,
                IEventSerializer serializer,
                string busName = null,
                Action<Unit> liveProcessingStarted = null,
                Action<SubscriptionDropReason, Exception> subscriptionDropped = null)
        {
            Bus = new InMemoryBus(busName ?? "Stream Listener");
            _streamStoreConnection = streamStoreConnection ?? throw new ArgumentNullException(nameof(streamStoreConnection));
            Settings = CatchUpSubscriptionSettings.Default;
            ListenerName = listenerName;
            _streamNameBuilder = streamNameBuilder;
            Serializer = serializer;
            _liveProcessingStarted = liveProcessingStarted;
            _subscriptionDropped = subscriptionDropped;
        }
        /// <summary>
        /// Event Stream Listener
        /// i.e. $et-[MessageType]
        /// </summary>
        /// <param name="tMessage"></param>
        /// <param name="checkpoint"></param>
        /// <param name="blockUntilLive"></param>
        /// <param name="cancelWaitToken">Cancellation token to cancel waiting if blockUntilLive is true</param>
        public void Start(
            Type tMessage,
            long? checkpoint = null,
            bool blockUntilLive = false,
            CancellationToken cancelWaitToken = default(CancellationToken))
        {
            if (!tMessage.IsSubclassOf(typeof(Event)))
            {
                throw new ArgumentException("type must derive from ReactiveDomain.Messaging.Event", nameof(tMessage));
            }
            Start(
               _streamNameBuilder.GenerateForEventType(tMessage.Name),
                checkpoint,
                blockUntilLive,
                cancelWaitToken);
        }
        /// <summary>
        /// Category Stream Listener
        /// i.e. $ce-[AggregateType]
        /// </summary>
        /// <typeparam name="TAggregate">The Aggregate type used to generate the stream name</typeparam>
        /// <param name="checkpoint"></param>
        /// <param name="blockUntilLive"></param>
        /// <param name="cancelWaitToken">Cancellation token to cancel waiting if blockUntilLive is true</param>
        public void Start<TAggregate>(
                        long? checkpoint = null,
                        bool blockUntilLive = false,
                        CancellationToken cancelWaitToken = default(CancellationToken)) where TAggregate : class, IEventSource
        {

            Start(
                _streamNameBuilder.GenerateForCategory(typeof(TAggregate)),
                checkpoint,
                blockUntilLive,
                cancelWaitToken);
        }

        /// <summary>
        /// Aggregate Stream listener
        /// i.e. [AggregateType]-[id]
        /// </summary>
        /// <typeparam name="TAggregate">The Aggregate type used to generate the stream name</typeparam>
        /// <param name="id"></param>
        /// <param name="checkpoint"></param>
        /// <param name="blockUntilLive"></param>
        /// <param name="cancelWaitToken">Cancellation token to cancel waiting if blockUntilLive is true</param>
        public void Start<TAggregate>(
                        Guid id,
                        long? checkpoint = null,
                        bool blockUntilLive = false,
                        CancellationToken cancelWaitToken = default(CancellationToken)) where TAggregate : class, IEventSource
        {
            Start(
                _streamNameBuilder.GenerateForAggregate(typeof(TAggregate), id),
                checkpoint,
                blockUntilLive,
                cancelWaitToken);
        }

        /// <summary>
        /// Custom Stream name
        /// i.e. [StreamName]
        /// </summary>
        /// <param name="streamName"></param>
        /// <param name="checkpoint"></param>
        /// <param name="blockUntilLive"></param>
        /// <param name="cancelWaitToken">Cancellation token to cancel waiting if blockUntilLive is true</param>
        public virtual void Start(
                            string streamName,
                            long? checkpoint = null,
                            bool blockUntilLive = false,
                            CancellationToken cancelWaitToken = default(CancellationToken))
        {
            _liveLock.Reset();
            lock (_startLock)
            {
                if (_started)
                    throw new InvalidOperationException("Listener already started.");
                if (!ValidateStreamName(streamName))
                    throw new ArgumentException("Stream not found.", streamName);
                StreamName = streamName;
                _subscription =
                    SubscribeToStreamFrom(
                        streamName,
                        checkpoint,
                        eventAppeared: GotEvent,
                        liveProcessingStarted: () =>
                        {
                            Bus.Publish(new StreamStoreMsgs.CatchupSubscriptionBecameLive());
                            _liveLock.Set();
                            _liveProcessingStarted?.Invoke(Unit.Default);
                        });
                _started = true;
            }
            if (blockUntilLive)
            {
                _liveLock.Wait(cancelWaitToken);
            }
        }
        public IDisposable SubscribeToStreamFrom(
            string stream,
            long? lastCheckpoint,
            Action<RecordedEvent> eventAppeared,
            Action liveProcessingStarted = null,
            Action<SubscriptionDropReason, Exception> subscriptionDropped = null,
            UserCredentials userCredentials = null)
        {
            StreamName = stream;
            Action<SubscriptionDropReason, Exception> dropped = (r, e) =>
            {
                _liveLock.Set();
                (subscriptionDropped ?? _subscriptionDropped)?.Invoke(r, e);
            };

            var sub = _streamStoreConnection.SubscribeToStreamFrom(
                stream,
                lastCheckpoint,
                Settings,
                eventAppeared,
                _ => liveProcessingStarted?.Invoke(),
                (reason, exception) => dropped(reason, exception),
                userCredentials);

            return new Disposer(() => { sub.Dispose(); return Unit.Default; });
        }

        public bool ValidateStreamName(string streamName)
        {
            var isValid = _streamStoreConnection.ReadStreamForward(streamName, 0, 1) != null;
            return isValid;
        }
        protected virtual void GotEvent(RecordedEvent recordedEvent)
        {
            Interlocked.Exchange(ref StreamPosition, recordedEvent.EventNumber);
            if (Serializer.Deserialize(recordedEvent) is IMessage @event)
            {
                Bus.Publish(@event);
            }
        }
        #region Implementation of IDisposable

        private bool _disposed;
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            _liveLock.Set();
            _subscription?.Dispose();
            Bus?.Dispose();
            _disposed = true;
        }
        #endregion
    }
}