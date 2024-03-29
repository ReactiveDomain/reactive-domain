﻿using System;
using System.Collections.Generic;
using System.Linq;

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation
{
    public class StreamStoreRepository : IRepository
    {

        public const string AggregateClrTypeHeader = "AggregateClrTypeName";
        public const string AggregateClrTypeNameHeader = "AggregateClrTypeNameHeader";
        public const string CommitIdHeader = "CommitId";
        public const string PolicyUserIdHeader = "PolicyUserId";
        private const int ReadPageSize = 500;

        private readonly IStreamNameBuilder _streamNameBuilder;
        private readonly IStreamStoreConnection _streamStoreConnection;
        private readonly IEventSerializer _eventSerializer;
        private readonly Func<Guid> _getPolicyUserId;

        public StreamStoreRepository(
            IStreamNameBuilder streamNameBuilder,
            IStreamStoreConnection eventStoreConnection,
            IEventSerializer eventSerializer,
            Func<Guid> getPolicyUserId = null)
        {
            _streamNameBuilder = streamNameBuilder;
            _streamStoreConnection = eventStoreConnection;
            _eventSerializer = eventSerializer;
            _getPolicyUserId = getPolicyUserId ?? (() => Guid.Empty);
        }

        public bool TryGetById<TAggregate>(Guid id, out TAggregate aggregate, int version = int.MaxValue) where TAggregate : class, IEventSource
        {
            try
            {
                aggregate = GetById<TAggregate>(id, version);
                return true;
            }
            catch (Exception)
            {
                aggregate = null;
                return false;
            }
        }
        public TAggregate GetById<TAggregate>(Guid id, int version = int.MaxValue) where TAggregate : class, IEventSource
        {

            if (version <= 0)
                throw new InvalidOperationException("Cannot get version <= 0");

            var streamName = _streamNameBuilder.GenerateForAggregate(typeof(TAggregate), id);
            var aggregate = ConstructAggregate<TAggregate>();


            long sliceStart = 0;
            StreamEventsSlice currentSlice;
            var appliedEventCount = 0;
            do
            {
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
        public void Update<TAggregate>(ref TAggregate aggregate, int version = int.MaxValue) where TAggregate : class, IEventSource
        {
            if (aggregate == null || aggregate?.Id == Guid.Empty) throw new ArgumentNullException(nameof(aggregate));
            if (version == aggregate.ExpectedVersion) return;
            if (version <= 0)
                throw new InvalidOperationException("Cannot get version <= 0");
            if (version < aggregate.ExpectedVersion)
            {
                throw new InvalidOperationException("Aggregate is ahead of version");
            }

            var streamName = _streamNameBuilder.GenerateForAggregate(typeof(TAggregate), aggregate.Id);
            long sliceStart = aggregate.ExpectedVersion + 1;
            StreamEventsSlice currentSlice;
            do
            {
                long sliceCount = sliceStart + ReadPageSize <= version
                                    ? ReadPageSize
                                    : version - sliceStart;

                currentSlice = _streamStoreConnection.ReadStreamForward(streamName, sliceStart, (int)sliceCount);

                if (currentSlice is StreamNotFoundSlice)
                    throw new AggregateNotFoundException(aggregate.Id, typeof(TAggregate));

                if (currentSlice is StreamDeletedSlice)
                    throw new AggregateDeletedException(aggregate.Id, typeof(TAggregate));

                sliceStart = currentSlice.NextEventNumber;

                aggregate.UpdateWithEvents(currentSlice.Events.Select(evt => _eventSerializer.Deserialize(evt)), aggregate.ExpectedVersion);

            } while (version > currentSlice.NextEventNumber && !currentSlice.IsEndOfStream);

            if (version != int.MaxValue && aggregate.ExpectedVersion != version - 1)
                throw new AggregateVersionException(aggregate.Id, typeof(TAggregate), version, aggregate.ExpectedVersion);
        }

        private static TAggregate ConstructAggregate<TAggregate>()
        {
            return (TAggregate)Activator.CreateInstance(typeof(TAggregate), true);
        }

        public void Save(IEventSource aggregate)
        {
            var commitHeaders = new Dictionary<string, object>
            {
                {CommitIdHeader, Guid.NewGuid() /*commitId*/},
                {AggregateClrTypeNameHeader, $"{aggregate.GetType().FullName},{aggregate.GetType().Assembly.GetName()}"},
                {AggregateClrTypeHeader, aggregate.GetType().Name}                
            };
            var policyUserId = _getPolicyUserId();
            if (policyUserId != Guid.Empty) { 
                commitHeaders.Add( PolicyUserIdHeader, policyUserId);
            }

            var streamName = _streamNameBuilder.GenerateForAggregate(aggregate.GetType(), aggregate.Id);
            var expectedVersion = aggregate.ExpectedVersion;
            var newEvents = aggregate.TakeEvents().ToArray();
            var eventsToSave = new EventData[newEvents.Length];
            for (int i = 0; i < newEvents.Length; i++)
            {
                eventsToSave[i] =
                    _eventSerializer.Serialize(
                        newEvents[i],
                        new Dictionary<string, object>(commitHeaders));
            }
            _streamStoreConnection.AppendToStream(streamName, expectedVersion, null, eventsToSave);
        }

        /// <summary>
        /// Soft delete the aggregate. Its stream can be re-created by appending new events.
        /// </summary>
        /// <param name="aggregate">The aggregate to be deleted.</param>
        public void Delete(IEventSource aggregate)
        {
            var streamName = _streamNameBuilder.GenerateForAggregate(aggregate.GetType(), aggregate.Id);
            var expectedVersion = aggregate.ExpectedVersion;
            _streamStoreConnection.DeleteStream(streamName, expectedVersion);
        }

        /// <summary>
        /// Hard delete the aggregate. This permanently deletes the aggregate's stream.
        /// </summary>
        /// <param name="aggregate">The aggregate to be deleted.</param>
        public void HardDelete(IEventSource aggregate)
        {
            var streamName = _streamNameBuilder.GenerateForAggregate(aggregate.GetType(), aggregate.Id);
            var expectedVersion = aggregate.ExpectedVersion;
            _streamStoreConnection.HardDeleteStream(streamName, expectedVersion);
        }
    }
}
