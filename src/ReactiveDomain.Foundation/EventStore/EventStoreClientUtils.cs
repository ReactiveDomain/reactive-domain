using System;
using System.Net;
using System.Text;
using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Logging;
using ILogger = ReactiveDomain.Messaging.Logging.ILogger;

namespace ReactiveDomain.Foundation.EventStore
{
    public static class EventStoreClientUtils
    {
        public const string EventStoreInstallationFolder = "EventStoreInstallationFolder";
        public const string EventClrTypeHeader = "EventClrTypeName";
        public const string CategoryStreamNamePrefix = @"$ce";
        public const string EventTypeStreamNamePrefix = @"$et";
        public const string LocalhostIp = "127.0.0.1";
        public const string EventStoreLogin = "admin";
        public const string DefaultEventStorePassword = "changeit";
        public const int EventStoreTcpPort = 1113;
        public const string EventStorePassword = "changeit";
        public const string EventStoreSubscriptionType = "EventStoreSubscriptionType";

        private static readonly ILogger Log = LogManager.GetLogger("EventStoreClientUtil");

        public static readonly IEventStoreConnection LocalEventStoreTcpConnection =
            EventStoreConnection.Create(new IPEndPoint(IPAddress.Parse(LocalhostIp), EventStoreTcpPort));

        public static UserCredentials EventStoreUserCredentials
        {
            get
            {
                var password = EventStorePassword;
                // If EventStorePassword is not defined in App.config, use the DefaultEventStorePassword.
                // There is a problem here. Authetication will fail 
                // if Event Store actaully does Authetication and the value of Consts.DefaultEventStorePassword differs from the actual Event Store password.
                // But it should not matter in devlopment environment.
                return password.GetEventStoreUserCredentials();
            }
        }

        public static UserCredentials GetEventStoreUserCredentials(this string password)
        {
            return new UserCredentials(EventStoreLogin, password);
        }

        public static string ToCamelCase(this string str)
        {
            return Char.ToLower(str[0]) + str.Substring(1);
        }

        public static string GetEventStreamNameByAggregatedId(this Type domainType, Guid aggregateId)
        {
            return $"{domainType.Name.ToCamelCase()}-{aggregateId.ToString("N")}";
        }

        public static string GetCategoryEventStreamName(this Type typeofAggregateDomainObject)
        {
            return $"{CategoryStreamNamePrefix}-{typeofAggregateDomainObject.Name.ToCamelCase()}";
        }
        public static string GetEventTypeStreamName(this Type typeofAggregateDomainObject)
        {
            return $"{EventTypeStreamNamePrefix}-{typeofAggregateDomainObject.Name}";
        }

        public static DomainEvent DeserializedDomainEvent(this ResolvedEvent @event)
        {
            return @event.DeserializeEvent() as DomainEvent;
        }

        public static object DeserializeEvent(this ResolvedEvent @event)
        {
            return @event.Event.Metadata.DeserializeEvent(@event.Event.Data);
        }

        public static object DeserializeEvent(this byte[] metadata, byte[] data)
        {
            JToken eventClrTypeName = JObject.Parse(Encoding.UTF8.GetString(metadata)).Property(EventClrTypeHeader).Value;
            var typeName = (string)eventClrTypeName;
            var type = Type.GetType(typeName);
            if (type == null)
            {
                throw new UnkownTypeException(typeName);
            }
            object @event = null;
            try
            {
                @event = JsonConvert.DeserializeObject(Encoding.UTF8.GetString(data), type, new JsonSerializerSettings { DefaultValueHandling = DefaultValueHandling.Populate });
            }
            catch (Exception ex)
            {
                var str = ex.Message;
            }
            
            return @event;
        }


        public static string GetStreamNameBasedOnDomainObjectType(
                                                                    this Type typeofDomainObject,
                                                                    bool resolveLinkTos = true,
                                                                    Guid aggregateId = default(Guid))
        {
            return resolveLinkTos ?
                                                        typeofDomainObject.GetCategoryEventStreamName()
                                                      : typeofDomainObject.GetEventStreamNameByAggregatedId(aggregateId);
        }

        public static EventStoreSubscription GetLiveOnlyEventStoreSubscription(
                                                    this IEventStoreConnection eventStoreConnection,
                                                    Type typeofDomainObject,
                                                    Action<EventStoreSubscription, ResolvedEvent> eventAppeared,
                                                    Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                                                    UserCredentials userCredentials = null,
                                                    bool resolveLinkTos = true,
                                                    Guid aggregateId = default(Guid))
        {
            try
            {
                var streamName = typeofDomainObject.GetStreamNameBasedOnDomainObjectType(
                                                                                        resolveLinkTos,
                                                                                        aggregateId);
                return eventStoreConnection.SubscribeToStreamAsync(
                                                                    streamName,
                                                                    resolveLinkTos,
                                                                    eventAppeared,
                                                                    subscriptionDropped,
                                                                    userCredentials).Result;
            }
            catch (Exception ex)
            {
                // Unlike a stream catch-up subscription, a live only subscription will fail if EventStore is not running.
                Log.Error(ex.Message);
                throw;
            }
        }

        public static EventStoreStreamCatchUpSubscription GetEventStoreStreamCatchUpSubscription(
                                                                  this IEventStoreConnection eventStoreConnection,
                                                                  Type typeofDomainObject,
                                                                  int? fromEventNumberExclusive,
                                                                  Action<EventStoreCatchUpSubscription, ResolvedEvent> eventAppeared,
                                                                  Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
                                                                  Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                                                                  UserCredentials userCredentials = null,
                                                                  bool resolveLinkTos = true,
                                                                  Guid aggregateId = default(Guid))
        {
            string stream = typeofDomainObject.GetStreamNameBasedOnDomainObjectType(resolveLinkTos, aggregateId);
            return eventStoreConnection.SubscribeToStreamFrom(
                                                                 stream,
                                                                 fromEventNumberExclusive,
                                                                 resolveLinkTos,
                                                                 eventAppeared,
                                                                 liveProcessingStarted,
                                                                 subscriptionDropped,
                                                                 userCredentials);
        }
    }

    public class UnkownTypeException : Exception
    {
        public UnkownTypeException(string typeName):base($"TypeName'{typeName}' was not found in the currently loaded appdomains.")
        {}
    }
}
