using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReactiveDomain.Foundation.EventStore;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Messaging.Util;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace ReactiveDomain.Foundation.Tests.EventStore
{
    public class MockCatchupStreamSubscriber : ICatchupStreamSubscriber
    {
        private const string EventClrTypeHeader = "EventClrTypeName";
        private readonly ISubscriber _eventSource;
        private readonly ReadOnlyDictionary<string, List<EventData>> _store;
        private readonly ReadOnlyCollection<Tuple<string, Message>> _history;

        public MockCatchupStreamSubscriber(
            ISubscriber eventSource,
            ReadOnlyDictionary<string, List<EventData>> store,
            ReadOnlyCollection<Tuple<string, Message>> history)
        {
            _eventSource = eventSource ?? throw new ArgumentNullException(nameof(eventSource));
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
            _eventSource.Subscribe<Message>(listener);
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
                    eventAppeared((Message)DeserializeEvent(evnt.Metadata, evnt.Data));
                }

                liveProcessingStarted?.Invoke();
            }

            return new SubscriptionDisposer(() =>
            {
                _eventSource.Unsubscribe(listener); return Unit.Default;
            });
        }

        public bool ValidateStreamName(string streamName)
        {
            return _store.ContainsKey(streamName);
        }

        private static object DeserializeEvent(byte[] metadata, byte[] data)
        {
            var eventClrTypeName = JObject.Parse(Encoding.UTF8.GetString(metadata)).Property(EventClrTypeHeader).Value;
            return JsonConvert.DeserializeObject(Encoding.UTF8.GetString(data), Type.GetType((string)eventClrTypeName));
        }
    }
}
