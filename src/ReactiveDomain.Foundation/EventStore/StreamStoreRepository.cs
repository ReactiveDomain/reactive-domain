using System;
using System.Collections.Generic;
using System.Linq;

// ReSharper disable MemberCanBePrivate.Global
namespace ReactiveDomain.Foundation.EventStore
{
    public class StreamStoreRepository : IRepository {
        public const string EventClrQualifiedTypeHeader = "EventClrQualifiedTypeName";
        public const string EventClrTypeHeader = "EventClrTypeName";
        public const string AggregateClrTypeHeader = "AggregateClrTypeName";
        public const string AggregateClrTypeNameHeader = "AggregateClrTypeNameHeader";
        public const string CommitIdHeader = "CommitId";
        private const int ReadPageSize = 500;

        private readonly IStreamNameBuilder _streamNameBuilder;
        private readonly IStreamStoreConnection _streamStoreConnection;
        private readonly IEventSerializer _eventSerializer;

        public StreamStoreRepository(
            IStreamNameBuilder streamNameBuilder,
            IStreamStoreConnection eventStoreConnection,
            IEventSerializer eventSerializer)
        {
            _streamNameBuilder = streamNameBuilder;
            _streamStoreConnection = eventStoreConnection;
            _eventSerializer = eventSerializer;
        }

        public bool TryGetById<TAggregate>(Guid id, out TAggregate aggregate) where TAggregate : class, IEventSource {
            return TryGetById(id, int.MaxValue, out aggregate);
        }

        public TAggregate GetById<TAggregate>(Guid id) where TAggregate : class, IEventSource {
            return GetById<TAggregate>(id, int.MaxValue);
        }

        public bool TryGetById<TAggregate>(Guid id, int version, out TAggregate aggregate) where TAggregate : class, IEventSource {
            try {
                aggregate = GetById<TAggregate>(id, version);
                return true;
            }
            catch (Exception) {
                aggregate = null;
                return false;
            }
        }

        public TAggregate GetById<TAggregate>(Guid id, int version) where TAggregate : class, IEventSource {
            if (version <= 0)
                throw new InvalidOperationException("Cannot get version <= 0");

            var streamName = _streamNameBuilder.GenerateForAggregate(typeof(TAggregate), id);
            var aggregate = ConstructAggregate<TAggregate>();


            long sliceStart = 0;
            StreamEventsSlice currentSlice;
            var appliedEventCount = 0;
            do {
                long sliceCount = sliceStart + ReadPageSize <= version
                                    ? ReadPageSize
                                    : version - sliceStart;

                currentSlice = _streamStoreConnection.ReadStreamForward(streamName, sliceStart, (int)sliceCount);

                if (currentSlice is StreamNotFoundSlice)
                    throw new AggregateNotFoundException(id, typeof(TAggregate));

                if (currentSlice is StreamDeletedSlice)
                    throw new AggregateDeletedException(id, typeof(TAggregate));

                sliceStart = currentSlice.NextEventNumber;

                foreach (var @event in currentSlice.Events) {
                    appliedEventCount++;
                    aggregate.RestoreFromEvent(DeserializeEvent(@event.Metadata, @event.Data));
                }

            } while (version > currentSlice.NextEventNumber && !currentSlice.IsEndOfStream);
            if (version != Int32.MaxValue && version != appliedEventCount)
                throw new AggregateVersionException(id, typeof(TAggregate), version, aggregate.ExpectedVersion);

            if (version != Int32.MaxValue && aggregate.ExpectedVersion != version - 1)
                throw new AggregateVersionException(id, typeof(TAggregate), version, aggregate.ExpectedVersion);

            return aggregate;

        }

        private static TAggregate ConstructAggregate<TAggregate>() {
            return (TAggregate)Activator.CreateInstance(typeof(TAggregate), true);
        }

        public object DeserializeEvent(byte[] metadata, byte[] data)
        {
            return _eventSerializer.Deserialize(metadata, data, EventClrQualifiedTypeHeader);
        }

        public void Save(IEventSource aggregate) {
            var commitHeaders = new Dictionary<string, object>
            {
                {CommitIdHeader, Guid.NewGuid() /*commitId*/},
                {AggregateClrTypeNameHeader, aggregate.GetType().AssemblyQualifiedName},
                {AggregateClrTypeHeader, aggregate.GetType().Name}
            };

            var streamName = _streamNameBuilder.GenerateForAggregate(aggregate.GetType(), aggregate.Id);
            var newEvents = aggregate.TakeEvents().ToList();
            var expectedVersion = aggregate.ExpectedVersion;
            var eventsToSave = newEvents.Select(e => ToEventData(Guid.NewGuid(), e, commitHeaders)).ToList();

            //n.b. write batching moved into Connection wrapper for eventstore
            _streamStoreConnection.AppendToStream(streamName, expectedVersion, null, eventsToSave.ToArray());

            //aggregate.ClearUncommittedEvents();
        }

        public EventData ToEventData(Guid eventId, object evnt, IDictionary<string, object> headers)
        {
            var data = _eventSerializer.Serialize(evnt);

            var eventHeaders = new Dictionary<string, object>(headers)
            {
                {EventClrTypeHeader, evnt.GetType().Name},
                {EventClrQualifiedTypeHeader, evnt.GetType().AssemblyQualifiedName}
            };
            var metadata = _eventSerializer.Serialize(eventHeaders);
            var typeName = evnt.GetType().Name;

            return new EventData(eventId, typeName, true, data, metadata);
        }
    }
}
