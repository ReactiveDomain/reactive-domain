using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using System.Reflection;
using System.Text;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using ReactiveDomain.Bus;
using ReactiveDomain.Domain;
using ReactiveDomain.Messaging;

// ReSharper disable MemberCanBePrivate.Global
namespace ReactiveDomain.EventStore
{
    public class GetEventStoreRepository : IRepository, ICatchupSteamSubscriber
    {
        public const string EventClrTypeHeader = "EventClrTypeName";
        public const string AggregateClrTypeHeader = "AggregateClrTypeName";
        public const string CommitIdHeader = "CommitId";
        private const int WritePageSize = 500;
        private const int ReadPageSize = 500;
        private IBus _outBus;

        private readonly Func<Type, Guid, string> _aggregateIdToStreamName;

        private readonly IEventStoreConnection _eventStoreConnection;
        private static readonly JsonSerializerSettings SerializerSettings;

        static GetEventStoreRepository()
        {
            SerializerSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None };
        }

        public GetEventStoreRepository(IEventStoreConnection eventStoreConnection, IBus outBus = null)
            : this(eventStoreConnection, (t, g) => string.Format("{0}-{1}", char.ToLower(t.Name[0]) + t.Name.Substring(1), g.ToString("N")), outBus)
        {
        }

        public GetEventStoreRepository(IEventStoreConnection eventStoreConnection, Func<Type, Guid, string> aggregateIdToStreamName, IBus outBus = null)
        {
            _outBus = outBus;
            _eventStoreConnection = eventStoreConnection;
            _aggregateIdToStreamName = aggregateIdToStreamName;
        }
        public bool TryGetById<TAggregate>(Guid id, out TAggregate aggregate) where TAggregate : class, IAggregate
        {
            return TryGetById(id, int.MaxValue, out aggregate);
        }
        public TAggregate GetById<TAggregate>(Guid id) where TAggregate : class, IAggregate
        {
            return GetById<TAggregate>(id, int.MaxValue);
        }
        public bool TryGetById<TAggregate>(Guid id, int version, out TAggregate aggregate) where TAggregate : class, IAggregate
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
        public TAggregate GetById<TAggregate>(Guid id, int version) where TAggregate : class, IAggregate
        {
            if (version <= 0)
                throw new InvalidOperationException("Cannot get version <= 0");

            var streamName = _aggregateIdToStreamName(typeof(TAggregate), id);
            var aggregate = ConstructAggregate<TAggregate>();

            long sliceStart = 0;
            StreamEventsSlice currentSlice;
            do
            {
                var sliceCount = sliceStart + ReadPageSize <= version
                                    ? ReadPageSize
                                    : version - sliceStart;
                //todo: why does this need an int with v 4.0 of eventstore?
                currentSlice = _eventStoreConnection.ReadStreamEventsForwardAsync(streamName, sliceStart, (int)sliceCount, false).Result;

                if (currentSlice.Status == SliceReadStatus.StreamNotFound)
                    throw new AggregateNotFoundException(id, typeof(TAggregate));

                if (currentSlice.Status == SliceReadStatus.StreamDeleted)
                    throw new AggregateDeletedException(id, typeof(TAggregate));

                sliceStart = currentSlice.NextEventNumber;

                foreach (var evnt in currentSlice.Events)
                    aggregate.ApplyEvent(DeserializeEvent(evnt.OriginalEvent.Metadata, evnt.OriginalEvent.Data));
            } while (version > currentSlice.NextEventNumber && !currentSlice.IsEndOfStream);


            if (aggregate.Version != version && version < Int32.MaxValue)
                throw new AggregateVersionException(id, typeof(TAggregate), aggregate.Version, version);
            return aggregate;

        }

        private static TAggregate ConstructAggregate<TAggregate>()
        {
            return (TAggregate)Activator.CreateInstance(typeof(TAggregate), true);
        }

        public static object DeserializeEvent(byte[] metadata, byte[] data)
        {
            var settings = new JsonSerializerSettings { ContractResolver = new ContractResolver() };
            var eventClrTypeName = JObject.Parse(Encoding.UTF8.GetString(metadata)).Property(EventClrTypeHeader).Value;
            return JsonConvert.DeserializeObject(Encoding.UTF8.GetString(data), Type.GetType((string)eventClrTypeName), settings);
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
            var newEvents = aggregate.GetUncommittedEvents().Cast<object>().ToList();
            var originalVersion = aggregate.Version - newEvents.Count;
            var expectedVersion = originalVersion == 0 ? ExpectedVersion.NoStream : originalVersion - 1;
            var eventsToSave = newEvents.Select(e => ToEventData(Guid.NewGuid(), e, commitHeaders)).ToList();

            if (eventsToSave.Count < WritePageSize)
            {
                _eventStoreConnection.AppendToStreamAsync(streamName, expectedVersion, eventsToSave).Wait();
            }
            else
            {
                var transaction = _eventStoreConnection.StartTransactionAsync(streamName, expectedVersion).Result;

                var position = 0;
                while (position < eventsToSave.Count)
                {
                    var pageEvents = eventsToSave.Skip(position).Take(WritePageSize);
                    transaction.WriteAsync(pageEvents).Wait();
                    position += WritePageSize;
                }

                transaction.CommitAsync().Wait();
            }
            if (_outBus != null)
                foreach (var evt in newEvents)
                {
                    try
                    {
                        _outBus.Publish((Message)evt);
                    }
                    catch { }//TODO: see if we need to do something here
                }
            aggregate.ClearUncommittedEvents();
        }

        public static EventData ToEventData(Guid eventId, object evnt, IDictionary<string, object> headers)
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

        public IListener GetListener(string name, bool sync = false)
        {
            return new SynchronizableStreamListener(name, this, sync);
        }
        #region Implementation of ICatchupSteamSubscriber

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
            var sub = _eventStoreConnection.SubscribeToStreamFrom(
                stream,
                lastCheckpoint,
                resolveLinkTos,
                (subscription, resolvedEvent) => eventAppeared(resolvedEvent.DeserializeEvent() as Message),
                _ => liveProcessingStarted?.Invoke(),
                (subscription, reason, exception) => subscriptionDropped?.Invoke(reason, exception),
                userCredentials,
                readBatchSize);

            return new SubscriptionDisposer(() => { sub.Stop(); return Unit.Default; });
        }

        public bool ValidateStreamName(string streamName)
        {
            return _eventStoreConnection.ReadStreamEventsForwardAsync(streamName, 0, 1, false).Result != null;
        }

        #endregion
    }
    public class ContractResolver : DefaultContractResolver
    {
        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);
            property.Writable = CanSetMemberValue(member, true);
            return property;
        }

        public static bool CanSetMemberValue(MemberInfo member, bool nonPublic)
        {
            switch (member.MemberType)
            {
                case MemberTypes.Field:
                    var fieldInfo = (FieldInfo)member;

                    return nonPublic || fieldInfo.IsPublic;
                case MemberTypes.Property:
                    var propertyInfo = (PropertyInfo)member;

                    if (!propertyInfo.CanWrite)
                        return false;
                    if (nonPublic)
                        return true;
                    return (propertyInfo.GetSetMethod(nonPublic) != null);
                default:
                    return false;
            }
        }
    }
}
