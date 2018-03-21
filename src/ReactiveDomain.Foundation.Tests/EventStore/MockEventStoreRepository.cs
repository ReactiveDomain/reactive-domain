using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReactiveDomain.Foundation.EventStore;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

// ReSharper disable MemberCanBePrivate.Global
namespace ReactiveDomain.Foundation.Tests.EventStore
{
    public class MockEventStoreRepository : IRepository
    {
        private const string EventClrTypeHeader = "EventClrTypeName";
        private const string AggregateClrTypeHeader = "AggregateClrTypeName";
        private const string CommitIdHeader = "CommitId";

        private readonly IStreamNameBuilder _streamNameBuilder;
        private readonly IPublisher _postCommitTarget;

        private readonly Dictionary<string, List<EventData>> _store;

        private readonly List<Tuple<string, Message>> _history;

        private static readonly JsonSerializerSettings SerializerSettings;

        public ReadOnlyDictionary<string, List<EventData>> Store => new ReadOnlyDictionary<string, List<EventData>>(_store);
        public ReadOnlyCollection<Tuple<string, Message>> History => _history.AsReadOnly();

        static MockEventStoreRepository()
        {
            SerializerSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None };
        }

        public MockEventStoreRepository(
            IStreamNameBuilder streamNameBuilder,
            IPublisher postCommitTarget = null)
        {
            _history = new List<Tuple<string, Message>>();
            _streamNameBuilder = streamNameBuilder;
            _postCommitTarget = postCommitTarget ?? new InMemoryBus("Post Commit Target");

            _store = new Dictionary<string, List<EventData>>();
        }

        public bool TryGetById<TAggregate>(Guid id, out TAggregate aggregate) where TAggregate : class, IEventSource
        {
            return TryGetById(id, int.MaxValue, out aggregate);
        }

        public bool TryGetById<TAggregate>(Guid id, int version, out TAggregate aggregate) where TAggregate : class, IEventSource
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

        public TAggregate GetById<TAggregate>(Guid id) where TAggregate : class, IEventSource
        {
            return GetById<TAggregate>(id, int.MaxValue);
        }

        public TAggregate GetById<TAggregate>(Guid id, int version) where TAggregate : class, IEventSource
        {
            if (version <= 0)
                throw new InvalidOperationException("Cannot get version <= 0");

            var streamName = _streamNameBuilder.GenerateForAggregate(typeof(TAggregate), id);
            var aggregate = ConstructAggregate<TAggregate>();

            if (!_store.TryGetValue(streamName, out var stream))
                throw new AggregateNotFoundException(id, typeof(TAggregate));

            var events = new List<Object>();
            foreach (var @event in stream.Take(version))
                events.Add(DeserializeEvent(@event.Metadata, @event.Data));

            aggregate.RestoreFromEvents(events);

            if (version != Int32.MaxValue && aggregate.ExpectedVersion != version - 1)
                throw new AggregateVersionException(id, typeof(TAggregate), version, aggregate.ExpectedVersion);

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

        public void Save(IEventSource aggregate, Action<IDictionary<string, object>> updateHeaders = null)
        {
            var commitHeaders = new Dictionary<string, object>
            {
                {CommitIdHeader, Guid.NewGuid() /*commitId*/},
                {AggregateClrTypeHeader, aggregate.GetType().AssemblyQualifiedName}
            };

            updateHeaders?.Invoke(commitHeaders);

            var streamName = _streamNameBuilder.GenerateForAggregate(aggregate.GetType(), aggregate.Id);
            var categoryStreamName = _streamNameBuilder.GenerateForCategory(aggregate.GetType());
            var newEvents = aggregate.TakeEvents().ToList();

            var eventsToSave = newEvents.Select(e => ToEventData(Guid.NewGuid(), e, commitHeaders)).ToList();

            _store.TryGetValue(streamName, out var stream);
            _store.TryGetValue(categoryStreamName, out var catStream);



            if (stream == null)
            {
                if (aggregate.ExpectedVersion == ExpectedVersion.Any || aggregate.ExpectedVersion == ExpectedVersion.NoStream)
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

            if (stream.Count != 0 && stream.Count - 1 != aggregate.ExpectedVersion) // a new stream will be @ version 0 
                throw new AggregateException(new WrongExpectedVersionException(
                    $"Stream {streamName} at version {stream.Count}, expected version {aggregate.ExpectedVersion}"));


            stream.AddRange(eventsToSave);
            catStream?.AddRange(eventsToSave);

            foreach (var evt in eventsToSave)
            {
                var etName = _streamNameBuilder.GenerateForEventType(evt.Type);
                if (!_store.TryGetValue(etName, out var etStream))
                {
                    etStream = new List<EventData>();
                    _store.Add(etName, etStream);
                }

                etStream.Add(evt);
            }

            foreach (var @event in newEvents.Where(@event => (@event as Message) != null))
            {
                _postCommitTarget?.Publish(@event as Message);
                _history.Add(new Tuple<string, Message>(streamName, @event as Message));
            }
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

        public void ReplayAllOnto(IBus bus)
        {
            foreach (var tupple in _history)
            {
                bus.Publish(tupple.Item2);
            }
        }
        public void ReplayStreamOnto(IBus bus, string streamName)
        {
            if (!_store.TryGetValue(streamName, out var events))
                return;
            foreach (var evnt in events)
            {
                bus?.Publish((Message)DeserializeEvent(evnt.Metadata, evnt.Data));
            }
        }

        public void ReplayCategoryOnto(IBus bus, string categoryName)
        {
            foreach (var tupple in _history)
            {
                if (tupple.Item1.Contains($"{categoryName}-")) // todo: not happy with this 'cause this depends on the stream name builder
                    bus?.Publish(tupple.Item2);
            }
        }
    }
}
