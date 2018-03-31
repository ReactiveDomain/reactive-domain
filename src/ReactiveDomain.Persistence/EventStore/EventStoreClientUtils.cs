using System;
using System.Net;
using EventStore.ClientAPI;



namespace ReactiveDomain.EventStore {
    /// <summary>
    /// WARNING: DO NOT use this to generate any normalized stream name. See StreamNameBuilder to create standard stream name instead.
    /// RD-33 will address this and will move some of the relevant methods to StreamNameBuilder.
    /// </summary>
    //TODO: Remove all of this
    [Obsolete]
    public static class EventStoreClientUtils {

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


        public static readonly IStreamStoreConnection LocalEventStoreTcpConnection =
           new EventStoreConnectionWrapper(EventStoreConnection.Create(new IPEndPoint(IPAddress.Parse(LocalhostIp), EventStoreTcpPort)));

        public static UserCredentials EventStoreUserCredentials {
            get {
                var password = EventStorePassword;
                // If EventStorePassword is not defined in App.config, use the DefaultEventStorePassword.
                // There is a problem here. Authetication will fail 
                // if Event Store actaully does Authetication and the value of Consts.DefaultEventStorePassword differs from the actual Event Store password.
                // But it should not matter in devlopment environment.
                return password.GetEventStoreUserCredentials();
            }
        }

        public static UserCredentials GetEventStoreUserCredentials(this string password) {
            return new UserCredentials(EventStoreLogin, password);
        }

        public static string ToCamelCaseInvariant(this string str) {
            return Char.ToLowerInvariant(str[0]) + str.Substring(1);
        }

        public static string GetEventStreamNameByAggregatedId(this Type domainType, Guid aggregateId) {
            return $"{domainType.Name.ToCamelCaseInvariant()}-{aggregateId.ToString("N")}";
        }

        public static string GetCategoryEventStreamName(this Type typeofAggregateDomainObject) {
            return $"{CategoryStreamNamePrefix}-{typeofAggregateDomainObject.Name.ToCamelCaseInvariant()}";
        }

        public static string GetEventTypeStreamName(this string typeOfEvent) {
            return $"{CategoryStreamNamePrefix}-{typeOfEvent.ToCamelCaseInvariant()}";
        }

        //public static DomainEvent DeserializedDomainEvent(this ResolvedEvent @event)
        //{
        //    return @event.DeserializeEvent() as DomainEvent;
        //}




        public static string GetStreamNameBasedOnDomainObjectType(
                                                                    this Type typeofDomainObject,
                                                                    bool resolveLinkTos = true,
                                                                    Guid aggregateId = default(Guid)) {
            return resolveLinkTos ?
                                                        typeofDomainObject.GetCategoryEventStreamName()
                                                      : typeofDomainObject.GetEventStreamNameByAggregatedId(aggregateId);
        }

        public static StreamSubscription GetLiveOnlyEventStoreSubscription(
                                                    this IStreamStoreConnection eventStoreConnection,
                                                    Type typeofDomainObject,
                                                    Action<StreamSubscription, RecordedEvent> eventAppeared,
                                                    Action<StreamSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                                                    UserCredentials userCredentials = null,
                                                    bool resolveLinkTos = true,
                                                    Guid aggregateId = default(Guid)) {

            // Unlike a stream catch-up subscription, a live only subscription will fail if EventStore is not running.
            var streamName = typeofDomainObject.GetStreamNameBasedOnDomainObjectType(
                                                                                    resolveLinkTos,
                                                                                    aggregateId);
            return eventStoreConnection.SubscribeToStream(
                                            streamName,
                                            eventAppeared,
                                            subscriptionDropped,
                                            userCredentials);
        }

        public static StreamCatchUpSubscription GetEventStoreStreamCatchUpSubscription(
                                                                  this IStreamStoreConnection eventStoreConnection,
                                                                  Type typeofDomainObject,
                                                                  long? fromEventNumberExclusive,
                                                                  Action<CatchUpSubscription, RecordedEvent> eventAppeared,
                                                                  Action<CatchUpSubscription> liveProcessingStarted = null,
                                                                  Action<CatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                                                                  UserCredentials userCredentials = null,
                                                                  bool resolveLinkTos = true,
                                                                  Guid aggregateId = default(Guid)) {



            string stream = typeofDomainObject.GetStreamNameBasedOnDomainObjectType(resolveLinkTos, aggregateId);
            return eventStoreConnection.SubscribeToStreamFrom(
                                                                 stream,
                                                                 fromEventNumberExclusive,
                                                                 CatchUpSubscriptionSettings.Default,
                                                                 eventAppeared,
                                                                 liveProcessingStarted,
                                                                 subscriptionDropped,
                                                                 userCredentials);
        }
    }
}
