using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using CommonDomain;
using CommonDomain.Persistence;
using EventStore.ClientAPI;
using Newtonsoft.Json.Linq;
using ReactiveDomain.Bus;
using ReactiveDomain.Messaging;

// ReSharper disable MemberCanBePrivate.Global
namespace ReactiveDomain.EventStore
{
    public class EventFileRepository : IRepository
    {
    //    private const string EventClrTypeHeader = "EventClrTypeName";
    //    private const string AggregateClrTypeHeader = "AggregateClrTypeName";
    //    private const string CommitIdHeader = "CommitId";
    //    private const int WritePageSize = 500;
    //    private const int ReadPageSize = 500;
    //    private readonly IBus _outBus;

    //    private readonly Func<Type, Guid, string> _aggregateIdToStreamName;

    //    private readonly DirectoryInfo _repository;
    //    private static readonly JsonSerializerSettings SerializerSettings;

    //    static EventFileRepository()
    //    {
    //        SerializerSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None };
    //    }

    //    public EventFileRepository(DirectoryInfo repository, IBus outBus = null)
    //        : this(repository, (t, g) => string.Format("{0}-{1}", char.ToLower(t.Name[0]) + t.Name.Substring(1), g.ToString("N")), outBus)
    //    {
    //    }

    //    public EventFileRepository(DirectoryInfo repository, Func<Type, Guid, string> aggregateIdToStreamName, IBus outBus = null)
    //    {
    //        _outBus = outBus;
    //        _repository = repository;
    //        _aggregateIdToStreamName = aggregateIdToStreamName;
    //    }

        public TAggregate GetById<TAggregate>(Guid id) where TAggregate : class, IAggregate
        {
            return GetById<TAggregate>(id, int.MaxValue);
        }

        public TAggregate GetById<TAggregate>(Guid id, int version) where TAggregate : class, IAggregate
        {
    //        if (version <= 0)
    //            throw new InvalidOperationException("Cannot get version <= 0");

    //        var streamName = _aggregateIdToStreamName(typeof(TAggregate), id);
            var aggregate = ConstructAggregate<TAggregate>();

    //        List<RecordedEvent> currentSlice = GetEvents(streamName, 0, version);
    //       // StreamEventsSlice currentSlice = _eventStoreConnection.ReadStreamEventsForward(streamName, 0, version, false);

    //        if(currentSlice == null)
    //        //if (currentSlice.Status == SliceReadStatus.StreamNotFound)
    //            throw new AggregateNotFoundException(id, typeof(TAggregate));

    //        //if (currentSlice.Status == SliceReadStatus.StreamDeleted)
    //        //    throw new AggregateDeletedException(id, typeof(TAggregate));


    //        foreach (var evnt in currentSlice)
    //            aggregate.ApplyEvent(DeserializeEvent(evnt.Metadata, evnt.Data));


    //        if (aggregate.Version != version && version < Int32.MaxValue)
    //            throw new AggregateVersionException(id, typeof(TAggregate), aggregate.Version, version);

            return aggregate;
        }

    //    private List<RecordedEvent> GetEvents(string streamName, int p, int version)
    //    {
    //        throw new NotImplementedException();
    //    }

        private static TAggregate ConstructAggregate<TAggregate>()
        {
            return (TAggregate)Activator.CreateInstance(typeof(TAggregate), true);
        }

    //    private static object DeserializeEvent(byte[] metadata, byte[] data)
    //    {
    //        var eventClrTypeName = JObject.Parse(Encoding.UTF8.GetString(metadata)).Property(EventClrTypeHeader).Value;
    //        return JsonConvert.DeserializeObject(Encoding.UTF8.GetString(data), Type.GetType((string)eventClrTypeName));
    //    }

        public void Save(IAggregate aggregate, Guid commitId, Action<IDictionary<string, object>> updateHeaders)
        {
    //        var commitHeaders = new Dictionary<string, object>
    //        {
    //            {CommitIdHeader, commitId},
    //            {AggregateClrTypeHeader, aggregate.GetType().AssemblyQualifiedName}
    //        };
    //        updateHeaders(commitHeaders);

    //        var streamName = _aggregateIdToStreamName(aggregate.GetType(), aggregate.Id);
    //        var newEvents = aggregate.GetUncommittedEvents().Cast<object>().ToList();
    //        var originalVersion = aggregate.Version - newEvents.Count;
    //        var expectedVersion = originalVersion == 0 ? ExpectedVersion.NoStream : originalVersion - 1;
    //        var eventsToSave = newEvents.Select(e => ToEventData(Guid.NewGuid(), e, commitHeaders)).ToList();

    //        if (eventsToSave.Count < WritePageSize)
    //        {
    //            _eventStoreConnection.AppendToStream(streamName, expectedVersion, eventsToSave);
    //        }
    //        else
    //        {
    //            var transaction = _eventStoreConnection.StartTransaction(streamName, expectedVersion);

    //            var position = 0;
    //            while (position < eventsToSave.Count)
    //            {
    //                var pageEvents = eventsToSave.Skip(position).Take(WritePageSize);
    //                transaction.Write(pageEvents);
    //                position += WritePageSize;
    //            }

    //            transaction.Commit();
    //        }
    //        if (_outBus != null)
    //            foreach (var evt in newEvents)
    //            {
    //                try
    //                {
    //                    _outBus.Publish((Message)evt);
    //                }
    //                catch { }//TODO: see if we need to do something here
    //            }
    //        aggregate.ClearUncommittedEvents();
        }

    //    private static EventData ToEventData(Guid eventId, object evnt, IDictionary<string, object> headers)
    //    {
    //        var data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(evnt, SerializerSettings));

    //        var eventHeaders = new Dictionary<string, object>(headers)
    //        {
    //            {
    //                EventClrTypeHeader, evnt.GetType().AssemblyQualifiedName
    //            }
    //        };
    //        var metadata = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(eventHeaders, SerializerSettings));
    //        var typeName = evnt.GetType().Name;

    //        return new EventData(eventId, typeName, true, data, metadata);
    //    }
    }
}
