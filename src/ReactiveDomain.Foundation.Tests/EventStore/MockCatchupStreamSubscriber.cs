using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using ReactiveDomain.Foundation.EventStore;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Messaging.Util;
using System;
using System.Collections.Generic;

namespace ReactiveDomain.Foundation.Tests.EventStore
{
    public class MockCatchupStreamSubscriber : ICatchupStreamSubscriber
    {
        private readonly IBus _bus;
        private readonly Dictionary<string, List<EventData>> _store;
        private readonly List<Tuple<string, Message>> _history;

        public MockCatchupStreamSubscriber(
            IBus bus, 
            Dictionary<string, List<EventData>> store,
            List<Tuple<string, Message>> history)
        {
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _history = history ?? throw new ArgumentNullException(nameof(history));
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
            var listener = new AdHocHandler<Message>(eventAppeared);
            _bus.Subscribe<Message>(listener);
            if (stream.StartsWith(EventStoreClientUtils.CategoryStreamNamePrefix)) //Category Stream
            {
                stream = stream.Split('-')[1];
                foreach (var tupple in _history)
                {
                    if (tupple.Item1.StartsWith(stream, StringComparison.Ordinal))
                        eventAppeared(tupple.Item2);
                }

                liveProcessingStarted?.Invoke();
            }
            else //standard stream
            {
                foreach (var evnt in _store[stream])
                {
                    eventAppeared((Message)MockEventStoreRepository.DeserializeEvent(evnt.Metadata, evnt.Data));
                }

                liveProcessingStarted?.Invoke();
            }

            return new SubscriptionDisposer(() =>
            {
                _bus.Unsubscribe(listener); return Unit.Default;
            });
        }

        public bool ValidateStreamName(string streamName)
        {
            return _store.ContainsKey(streamName);
        }
    }
}
