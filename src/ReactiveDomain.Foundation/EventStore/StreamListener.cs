using System;
using System.Threading;
using EventStore.ClientAPI;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Foundation.EventStore
{
    public class StreamListener : IListener
    {
        protected readonly string ListenerName;
        private readonly ICatchupStreamSubscriber _subscriptionTarget;


        private InMemoryBus _bus;
        IDisposable _subscription;
        private bool _started;
        private readonly StreamNameBuilder _streamNameBuilder;
        private readonly object _startlock = new object();
        private readonly ManualResetEventSlim _liveLock = new ManualResetEventSlim();
        public ISubscriber EventStream => _bus;
        

        /// <summary>
        /// For listening to generic streams 
        /// </summary>
        /// <param name="listenerName"></param>
        /// <param name="subscriptionTarget">The target to subscribe to</param>
        /// <param name="busName">The name to use for the internal bus (helpful in debugging)</param>
        public StreamListener(string listenerName, ICatchupStreamSubscriber subscriptionTarget, StreamNameBuilder streamNameBuilder, string busName = null)
        {
            _bus = new InMemoryBus(busName ?? "Stream Listener");
            _subscriptionTarget = subscriptionTarget;
            ListenerName = listenerName;
            _streamNameBuilder = streamNameBuilder;
        }
        /// <summary>
        /// Category Stream Listener
        /// i.e. $ce-[AggregateType]
        /// </summary>
        /// <typeparam name="TAggregate"></typeparam>
        /// <param name="checkpoint"></param>
        /// <param name="blockUntilLive"></param>
        public void Start<TAggregate>(int? checkpoint = null, bool blockUntilLive = false, int millisecondsTimeout = 1000) where TAggregate : class, IEventSource
        {
            Start(_streamNameBuilder.GenerateForCategory(typeof(TAggregate)), checkpoint, blockUntilLive, millisecondsTimeout);
        }
        /// <summary>
        /// Aggregate Stream listener
        /// i.e. [AggregateType]-[id]
        /// </summary>
        /// <typeparam name="TAggregate"></typeparam>
        /// <param name="id"></param>
        /// <param name="checkpoint"></param>
        /// <param name="blockUntilLive"></param>
        public void Start<TAggregate>(Guid id, int? checkpoint = null, bool blockUntilLive = false, int millisecondsTimeout = 1000) where TAggregate : class, IEventSource
        {
            Start(_streamNameBuilder.GenerateForAggregate(typeof(TAggregate), id), checkpoint, blockUntilLive, millisecondsTimeout);
        }

        /// <summary>
        /// Custom Stream name
        /// i.e. [StreamName]
        /// </summary>
        /// <param name="streamName"></param>
        /// <param name="checkpoint"></param>
        /// <param name="blockUntilLive"></param>
        public virtual void Start(string streamName, int? checkpoint = null, bool blockUntilLive = false, int millisecondsTimeout = 1000)
        {
            _liveLock.Reset();
            lock (_startlock)
            {
                if (_started)
                    throw new InvalidOperationException("Listener already started.");
                if (!_subscriptionTarget.ValidateStreamName(streamName))
                    throw new ArgumentException("Stream not found.", streamName);

                _subscription =
                    _subscriptionTarget.SubscribeToStreamFrom(
                        streamName,
                        checkpoint ?? StreamCheckpoint.StreamStart,
                        true,
                        eventAppeared: GotEvent,
                        liveProcessingStarted: () =>
                        {
                            _bus.Publish(new EventStoreMsg.CatchupSubscriptionBecameLive());
                            _liveLock.Set();
                        });
                _started = true;
            }
            if (blockUntilLive)
                _liveLock.Wait(millisecondsTimeout);
        }
        protected virtual void GotEvent(Message @event)
        {
            if (@event != null) _bus.Publish(@event);
        }
        #region Implementation of IDisposable

        private bool _disposed = false;
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
            _bus?.Dispose();
            _disposed = true;
        }
        #endregion
    }
}
