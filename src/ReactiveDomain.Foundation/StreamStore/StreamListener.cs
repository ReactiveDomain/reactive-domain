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
    /// Subscription and are not guaranteed to be call in order, especially if handlers require variable
    /// amounts of time to complete processing.
    /// If event ordering is required use the QueuedListener.
    /// </remarks> 
    public class StreamListener : IListener
    {
        protected readonly string ListenerName;
        protected readonly InMemoryBus Bus;
        IDisposable _subscription;
        private bool _started;
        private readonly IStreamNameBuilder _streamNameBuilder;
        protected readonly IEventSerializer Serializer;
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
        public StreamListener(
                string listenerName,
                IStreamStoreConnection streamStoreConnection,
                IStreamNameBuilder streamNameBuilder,
                IEventSerializer serializer,
                string busName = null)
        {
            Bus = new InMemoryBus(busName ?? "Stream Listener");
            _streamStoreConnection = streamStoreConnection ?? throw new ArgumentNullException(nameof(streamStoreConnection));
            Settings = CatchUpSubscriptionSettings.Default;
            ListenerName = listenerName;
            _streamNameBuilder = streamNameBuilder;
            Serializer = serializer;
        }
        /// <summary>
        /// Event Stream Listener
        /// i.e. $et-[MessageType]
        /// </summary>
        /// <param name="tMessage"></param>
        /// <param name="checkpoint"></param>
        /// <param name="blockUntilLive"></param>
        /// <param name="millisecondsTimeout"></param>
        public void Start(
            Type tMessage,
            long? checkpoint = null,
            bool blockUntilLive = false,
            int millisecondsTimeout = 1000)
        {
            if (!tMessage.IsSubclassOf(typeof(Event)))
            {
                throw new ArgumentException("type must derive from ReactiveDomain.Messaging.Event", nameof(tMessage));
            }
            Start(
               _streamNameBuilder.GenerateForEventType(tMessage.Name),
                checkpoint,
                blockUntilLive,
                millisecondsTimeout);
        }
        /// <summary>
        /// Category Stream Listener
        /// i.e. $ce-[AggregateType]
        /// </summary>
        /// <typeparam name="TAggregate">The Aggregate type used to generate the stream name</typeparam>
        /// <param name="checkpoint"></param>
        /// <param name="blockUntilLive"></param>
        /// <param name="timeout">timeout in milliseconds default = 1000</param>
        public void Start<TAggregate>(
                        long? checkpoint = null,
                        bool blockUntilLive = false,
                        int timeout = 1000) where TAggregate : class, IEventSource
        {

            Start(
                _streamNameBuilder.GenerateForCategory(typeof(TAggregate)),
                checkpoint,
                blockUntilLive,
                timeout);
        }

        /// <summary>
        /// Aggregate Stream listener
        /// i.e. [AggregateType]-[id]
        /// </summary>
        /// <typeparam name="TAggregate">The Aggregate type used to generate the stream name</typeparam>
        /// <param name="id"></param>
        /// <param name="checkpoint"></param>
        /// <param name="blockUntilLive"></param>
        /// <param name="timeout">timeout in milliseconds default = 1000</param>
        public void Start<TAggregate>(
                        Guid id,
                        long? checkpoint = null,
                        bool blockUntilLive = false,
                        int timeout = 1000) where TAggregate : class, IEventSource
        {
            Start(
                _streamNameBuilder.GenerateForAggregate(typeof(TAggregate), id),
                checkpoint,
                blockUntilLive,
                timeout);
        }

        /// <summary>
        /// Custom Stream name
        /// i.e. [StreamName]
        /// </summary>
        /// <param name="streamName"></param>
        /// <param name="checkpoint"></param>
        /// <param name="blockUntilLive"></param>
        /// <param name="timeout">timeout in milliseconds default = 1000</param>
        public virtual void Start(
                            string streamName,
                            long? checkpoint = null,
                            bool blockUntilLive = false,
                            int timeout = 1000)
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
                        true,
                        eventAppeared: GotEvent,
                        liveProcessingStarted: () => {
                            Bus.Publish(new StreamStoreMsgs.CatchupSubscriptionBecameLive());
                            _liveLock.Set();
                        });
                _started = true;
            }
            if (blockUntilLive)
                _liveLock.Wait(timeout);
        }
        public IDisposable SubscribeToStreamFrom(
            string stream,
            long? lastCheckpoint,
            bool resolveLinkTos,
            Action<RecordedEvent> eventAppeared,
            Action liveProcessingStarted = null,
            Action<SubscriptionDropReason, Exception> subscriptionDropped = null,
            UserCredentials userCredentials = null,
            int readBatchSize = 500)
        {
            var settings = new CatchUpSubscriptionSettings(10, readBatchSize, false);
            StreamName = stream;
            var sub = _streamStoreConnection.SubscribeToStreamFrom(
                stream,
                lastCheckpoint,
                settings,
                eventAppeared,
                _ => liveProcessingStarted?.Invoke(),
                (reason, exception) => subscriptionDropped?.Invoke(reason, exception),
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
            if (Serializer.Deserialize(recordedEvent) is Message @event)
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

            _subscription?.Dispose();
            Bus?.Dispose();
            _disposed = true;
        }
        #endregion
    }
}