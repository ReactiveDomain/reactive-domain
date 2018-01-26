using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Text;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReactiveDomain.Foundation.EventStore;
using ReactiveDomain.Legacy;
using ReactiveDomain.Legacy.CommonDomain;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using Unit = ReactiveDomain.Messaging.Util.Unit;

// ReSharper disable MemberCanBePrivate.Global
namespace ReactiveDomain.Tests.Mocks
{
    public class MockEventStoreRepository : IRepository, ISubscriber, ICatchupSteamSubscriber
    {
        private readonly IBus _bus;
        private const string EventClrTypeHeader = "EventClrTypeName";
        private const string AggregateClrTypeHeader = "AggregateClrTypeName";
        private const string CommitIdHeader = "CommitId";

        private readonly Func<Type, Guid, string> _aggregateIdToStreamName;
        private readonly Func<Type, string> _aggregateTypeToCategoryStreamName;
        private readonly Func<string, string> _eventNameToEventTypeStreamName;

        private readonly Dictionary<string, List<EventData>> _store = new Dictionary<string, List<EventData>>();

        private readonly List<Tuple<string, Message>> _history;

        private static readonly JsonSerializerSettings SerializerSettings;

        static MockEventStoreRepository()
        {
            SerializerSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None };
        }

        public MockEventStoreRepository(IBus bus = null)
            : this((t, g) => $"{char.ToLower(t.Name[0]) + t.Name.Substring(1)}-{g.ToString("N")}")
        {
            _bus = bus ?? new InMemoryBus("Mock Repository Bus");
            _history = new List<Tuple<string, Message>>();
        }

        public MockEventStoreRepository(Func<Type, Guid, string> aggregateIdToStreamName)
        {
            _aggregateIdToStreamName = aggregateIdToStreamName;
            _aggregateTypeToCategoryStreamName = t => $"$ce-{char.ToLower(t.Name[0]) + t.Name.Substring(1)}";
            _eventNameToEventTypeStreamName = name => $"$et-{name}";
        }

        public bool TryGetById<TAggregate>(Guid id, out TAggregate aggregate) where TAggregate : class, IAggregate
        {
            return TryGetById(id, int.MaxValue, out aggregate);
        }

        public bool TryGetById<TAggregate>(Guid id, int version, out TAggregate aggregate) where TAggregate : class, IAggregate
        {
            aggregate = null;
            try
            {
                aggregate = GetById<TAggregate>(id, version);
                return true;
            }
            catch (Exception)
            {
                return false;
            }

        }

        public TAggregate GetById<TAggregate>(Guid id) where TAggregate : class, IAggregate
        {
            return GetById<TAggregate>(id, int.MaxValue);
        }

        public TAggregate GetById<TAggregate>(Guid id, int version) where TAggregate : class, IAggregate
        {
            if (version <= 0)
                throw new InvalidOperationException("Cannot get version <= 0");

            var streamName = _aggregateIdToStreamName(typeof(TAggregate), id);
            var aggregate = ConstructAggregate<TAggregate>();

            List<EventData> stream;

            if (!_store.TryGetValue(streamName, out stream))
                throw new AggregateNotFoundException(id, typeof(TAggregate));

            foreach (var evnt in stream.Take(version))
                aggregate.ApplyEvent(DeserializeEvent(evnt.Metadata, evnt.Data));

            if (aggregate.Version != version && version < Int32.MaxValue)
                throw new AggregateVersionException(id, typeof(TAggregate), aggregate.Version, version);

            return aggregate;
        }

        private static TAggregate ConstructAggregate<TAggregate>()
        {
            return (TAggregate)Activator.CreateInstance(typeof(TAggregate), true);
        }

        private static object DeserializeEvent(byte[] metadata, byte[] data)
        {
            var eventClrTypeName = JObject.Parse(Encoding.UTF8.GetString(metadata)).Property(EventClrTypeHeader).Value;
            return JsonConvert.DeserializeObject(Encoding.UTF8.GetString(data), Type.GetType((string)eventClrTypeName));
        }

        public void Save(IAggregate aggregate, Guid commitId, Action<IDictionary<string, object>> updateHeaders)
        {
            var commitHeaders = new Dictionary<string, object>
            {
                {CommitIdHeader, commitId},
                {AggregateClrTypeHeader, aggregate.GetType().AssemblyQualifiedName}
            };
            updateHeaders(commitHeaders);

            var streamName = _aggregateIdToStreamName(aggregate.GetType(), aggregate.Id);
            var categoryStreamName = _aggregateTypeToCategoryStreamName(aggregate.GetType());
            var newEvents = aggregate.GetUncommittedEvents().Cast<object>().ToList();
            var originalVersion = aggregate.Version - newEvents.Count;
            var expectedVersion = originalVersion == 0 ? ExpectedVersion.NoStream : originalVersion - 1;
            var eventsToSave = newEvents.Select(e => ToEventData(Guid.NewGuid(), e, commitHeaders)).ToList();

            List<EventData> stream;
            _store.TryGetValue(streamName, out stream);
            List<EventData> catStream;
            _store.TryGetValue(categoryStreamName, out catStream);



            if (stream == null)
            {
                if (expectedVersion == ExpectedVersion.Any || expectedVersion == ExpectedVersion.NoStream)
                {
                    stream = new List<EventData>();
                    _store.Add(streamName, stream);
                    if (catStream == null)
                    {
                        catStream = new List<EventData>();
                        _store.Add(categoryStreamName, catStream);
                    }
                }
                else throw new WrongExpectedVersionException("Stream " + streamName + " does not exist.");
            }

            if (stream.Count != 0 && stream.Count - 1 != expectedVersion) // a new stream will be @ version 0 
                throw new AggregateException(new WrongExpectedVersionException(
                    $"Stream {streamName} at version {stream.Count}, expected version {expectedVersion}"));


            stream.AddRange(eventsToSave);
            catStream?.AddRange(eventsToSave);

            foreach (var evt in eventsToSave)
            {
                var etName = _eventNameToEventTypeStreamName(evt.Type);
                List<EventData> etStream;
                if (!_store.TryGetValue(etName, out etStream))
                {
                    etStream = new List<EventData>();
                    _store.Add(etName, etStream);
                }
                etStream.Add(evt);
            }

            foreach (var @event in aggregate.GetUncommittedEvents().Cast<object>().Where(@event => (@event as Message) != null))
            {
                _bus.Publish(@event as Message);
                _history.Add(new Tuple<string, Message>(streamName, @event as Message));
            }
            aggregate.ClearUncommittedEvents();
        }



        private static EventData ToEventData(Guid eventId, object evnt, IDictionary<string, object> headers)
        {
            var data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(evnt, SerializerSettings));

            var eventHeaders = new Dictionary<string, object>(headers)
            {
                {
                    EventClrTypeHeader, evnt.GetType().AssemblyQualifiedName
                }
            };
            var metadata = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(eventHeaders, SerializerSettings));
            var typeName = evnt.GetType().Name;

            return new EventData(eventId, typeName, true, data, metadata);
        }

        public IDisposable Subscribe<T>(IHandle<T> handler) where T : Message
        {
            _bus.Subscribe(handler);
            return new SubscriptionDisposer(() => { this.Unsubscribe(handler); return Unit.Default; });
        }

        public void Unsubscribe<T>(IHandle<T> handler) where T : Message
        {
            _bus.Unsubscribe(handler);
        }

        public bool HasSubscriberFor<T>(bool includeDerived = false) where T : Message
        {
            return _bus.HasSubscriberFor<T>(includeDerived);
        }

        public void ReplayAllOnto(IBus bus)
        {
            foreach (var tupple in _history)
            {
                bus.Publish(tupple.Item2);
            }
        }
        public void ReplayStreamOnto(IBus bus, string streamName)
        {
            foreach (var evnt in _store[streamName])
            {
                bus.Publish((Message)DeserializeEvent(evnt.Metadata, evnt.Data));
            }
        }
        public void ReplayCategoryOnto(IBus bus, string categoryName)
        {
            foreach (var tupple in _history)
            {
                if (tupple.Item1.StartsWith(categoryName, StringComparison.Ordinal))
                    bus.Publish(tupple.Item2);
            }
        }
        public IListener GetListener(string name, bool sync = false)
        {
            return new SynchronizableStreamListener(name, this, sync);
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
                    eventAppeared((Message)DeserializeEvent(evnt.Metadata, evnt.Data));
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
