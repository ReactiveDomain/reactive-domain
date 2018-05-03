using System;
using System.Collections.Generic;
using System.Linq;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation
{
    public class StreamStoreRepository : IRepository {
       
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

                appliedEventCount += currentSlice.Events.Length;
                aggregate.RestoreFromEvents(currentSlice.Events.Select(evt => _eventSerializer.Deserialize(evt)));

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

       

        public void Save(IEventSource aggregate) {
            var commitHeaders = new Dictionary<string, object>
            {
                {CommitIdHeader, Guid.NewGuid() /*commitId*/},
                {AggregateClrTypeNameHeader, aggregate.GetType().AssemblyQualifiedName},
                {AggregateClrTypeHeader, aggregate.GetType().Name}
            };

            var streamName = _streamNameBuilder.GenerateForAggregate(aggregate.GetType(), aggregate.Id);
            var newEvents = aggregate.TakeEvents().ToArray();
            var expectedVersion = aggregate.ExpectedVersion;
            var eventsToSave = new EventData[newEvents.Length];
            for (int i = 0; i < newEvents.Length; i++) {
                eventsToSave[i] =
                    _eventSerializer.Serialize(
                        newEvents[i], 
                        new Dictionary<string, object>(commitHeaders));
            }
            _streamStoreConnection.AppendToStream(streamName, expectedVersion, null, eventsToSave);
        }

      
    }
}
