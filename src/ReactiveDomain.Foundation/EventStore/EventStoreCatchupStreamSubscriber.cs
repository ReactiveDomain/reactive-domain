using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Messaging.Util;
using System;

namespace ReactiveDomain.Foundation.EventStore
{
    public class EventStoreCatchupStreamSubscriber : ICatchupStreamSubscriber
    {
        private readonly IEventStoreConnection _eventStoreConnection;

        public EventStoreCatchupStreamSubscriber(IEventStoreConnection eventStoreConnection)
        {
            _eventStoreConnection = eventStoreConnection ?? throw new ArgumentNullException(nameof(eventStoreConnection));
        }

        public IDisposable SubscribeToStreamFrom(
            string stream,
            int? lastCheckpoint,
            bool resolveLinkTos,
            Action<Message> eventAppeared,
            Action liveProcessingStarted = null,
            Action<SubscriptionDropReason, Exception> subscriptionDropped = null,
            UserCredentials userCredentials = null,
            int readBatchSize = 500)
        {
            var settings = new CatchUpSubscriptionSettings(10, readBatchSize, false, true);
            var sub = _eventStoreConnection.SubscribeToStreamFrom(
                stream,
                lastCheckpoint,
                settings,
                (subscription, resolvedEvent) => eventAppeared(resolvedEvent.DeserializeEvent() as Message),
                _ => liveProcessingStarted?.Invoke(),
                (subscription, reason, exception) => subscriptionDropped?.Invoke(reason, exception),
                userCredentials);

            return new SubscriptionDisposer(() => { sub.Stop(); return Unit.Default; });
        }

        public bool ValidateStreamName(string streamName)
        {
            return _eventStoreConnection.ReadStreamEventsForwardAsync(streamName, 0, 1, false).Result != null;
        }
    }
}
