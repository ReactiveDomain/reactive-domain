using System;
using System.Threading.Tasks;
using EventStore.ClientAPI;

namespace ReactiveDomain.EventStore
{
    public class ESConnection:IStreamStoreConnection
    {
        private readonly IEventStoreConnection _conn;

        public ESConnection(IEventStoreConnection conn) {
            _conn = conn;
        }

        public async Task<StreamEventsSlice> ReadStreamEventsForwardAsync(string stream, long start, int count, bool resolveLinkTos,
                                                       UserCredentials credentials = null) {
            throw new NotImplementedException();
        }

        public async Task<WriteResult> AppendToStreamAsync(string stream, long expectedVersion, params EventData[] events) {
            throw new NotImplementedException();
        }

        public async Task<EventStoreSubscription> SubscribeToStreamAsync(string stream, bool resolveLinkTos, Action<EventStoreSubscription, ResolvedEvent> eventAppeared, Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                                                 UserCredentials userCredentials = null) {
            throw new NotImplementedException();
        }

        public EventStoreStreamCatchUpSubscription SubscribeToStreamFrom(string stream, long? lastCheckpoint,
                                                                         CatchUpSubscriptionSettings settings, Action<EventStoreCatchUpSubscription, ResolvedEvent> eventAppeared,
                                                                         Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
                                                                         Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                                                                         UserCredentials userCredentials = null) {
            throw new NotImplementedException();
        }

        public async Task ConnectAsync() {
            throw new NotImplementedException();
        }

        public void Close() {
            throw new NotImplementedException();
        }

        public EventHandler<ClientConnectionEventArgs> Connected { get; set; }

        public async Task<EventStoreSubscription> SubscribeToAllAsync(bool resolveLinkTos, Action<EventStoreSubscription, ResolvedEvent> eventAppeared, Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
                                              UserCredentials userCredentials = null) {
            throw new NotImplementedException();
        }

        public async Task<EventStoreTransaction> StartTransactionAsync(string stream, long expectedVersion, UserCredentials userCredentials = null) {
            throw new NotImplementedException();
        }

        public async Task<DeleteResult> DeleteStreamAsync(StreamName stream, int expectedVersion) {
            throw new NotImplementedException();
        }

        public void Dispose() {
            _conn?.Close();
            _conn?.Dispose();
        }
    }
}
