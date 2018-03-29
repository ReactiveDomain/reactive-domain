using System;
using System.Threading.Tasks;


namespace ReactiveDomain {
    public interface IStreamStoreConnection:IDisposable {
        /// <summary>
        /// Asynchronously reads count events from an event stream forwards (e.g. oldest to newest) starting from position start.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="start">The starting point to read from.</param>
        /// <param name="count">The count of items to read.</param>
        /// <param name="resolveLinkTos">Whether to resolve LinkTo events automatically.</param>
        /// <param name="credentials">The user credentials</param>
        /// <returns>A <see cref="T:System.Threading.Tasks.Task`1" /> containing the results of the read operation.</returns>
        Task<StreamEventsSlice> ReadStreamEventsForwardAsync(string stream, long start, int count, bool resolveLinkTos, UserCredentials credentials = null);
        /// <summary>Appends events asynchronously to a stream.</summary>
        /// <remarks>
        /// When appending events to a stream the <see cref="T:EventStore.ClientAPI.ExpectedVersion" /> choice can
        /// make a large difference in the observed behavior. For example, if no stream exists
        /// and ExpectedVersion.Any is used, a new stream will be implicitly created when appending.
        
        /// There are also differences in idempotency between different types of calls.
        /// If you specify an ExpectedVersion aside from ExpectedVersion.Any, Event Store
        /// will give you an idempotency guarantee. If using ExpectedVersion.Any, Event Store
        /// will do its best to provide idempotency but does not guarantee idempotency.
        /// </remarks>
        /// <param name="stream">The name of the stream to append events to.</param>
        /// <param name="expectedVersion">The <see cref="T:EventStore.ClientAPI.ExpectedVersion" /> of the stream to append to.</param>
        /// <param name="events">The events to append to the stream.</param>
        /// <returns>A <see cref="T:System.Threading.Tasks.Task`1" /> containing the results of the write operation.</returns>
        Task<WriteResult> AppendToStreamAsync(string stream, long expectedVersion, params EventData[] events);
       
        /// <summary>
        /// Asynchronously subscribes to a single event stream. New events
        /// written to the stream while the subscription is active will be
        /// pushed to the client.
        /// </summary>
        /// <param name="stream">The stream to subscribe to.</param>
        /// <param name="resolveLinkTos">Whether to resolve Link events automatically.</param>
        /// <param name="eventAppeared">A Task invoked and awaited when a new event is received over the subscription.</param>
        /// <param name="subscriptionDropped">An action invoked if the subscription is dropped.</param>
        /// <param name="userCredentials">User credentials to use for the operation.</param>
        /// <returns>A <see cref="T:System.Threading.Tasks.Task`1" /> representing the subscription.</returns>
        Task<EventStoreSubscription> SubscribeToStreamAsync(
            string stream, 
            bool resolveLinkTos, 
            Action<EventStoreSubscription, ResolvedEvent> eventAppeared, 
            Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null, 
            UserCredentials userCredentials = null);
         /// <summary>
    /// Subscribes to a single event stream. Existing events from
    /// lastCheckpoint onwards are read from the stream
    /// and presented to the user of <see cref="T:EventStore.ClientAPI.EventStoreCatchUpSubscription" />
    /// as if they had been pushed.
    /// 
    /// Once the end of the stream is read the subscription is
    /// transparently (to the user) switched to push new events as
    /// they are written.
    /// 
    /// The action liveProcessingStarted is called when the
    /// <see cref="T:EventStore.ClientAPI.EventStoreCatchUpSubscription" /> switches from the reading
    /// phase to the live subscription phase.
    /// </summary>
    /// <param name="stream">The stream to subscribe to.</param>
    /// <param name="lastCheckpoint">The event number from which to start.
    /// 
    /// To receive all events in the stream, use <see cref="F:EventStore.ClientAPI.StreamCheckpoint.StreamStart" />.
    /// If events have already been received and resubscription from the same point
    /// is desired, use the event number of the last event processed which
    /// appeared on the subscription.
    /// 
    /// Using <see cref="F:EventStore.ClientAPI.StreamPosition.Start" /> here will result in missing
    /// the first event in the stream.</param>
    /// <param name="eventAppeared">A Task invoked and awaited when a new event is received over the subscription.</param>
    /// <param name="liveProcessingStarted">An action invoked when the subscription switches to newly-pushed events.</param>
    /// <param name="subscriptionDropped">An action invoked if the subscription is dropped.</param>
    /// <param name="userCredentials">User credentials to use for the operation.</param>
    /// <param name="settings">The <see cref="T:EventStore.ClientAPI.CatchUpSubscriptionSettings" /> for the subscription.</param>
    /// <returns>An <see cref="T:EventStore.ClientAPI.EventStoreStreamCatchUpSubscription" /> representing the subscription.</returns>
    EventStoreStreamCatchUpSubscription SubscribeToStreamFrom(
             string stream, 
             long? lastCheckpoint, 
             CatchUpSubscriptionSettings settings, 
             Action<EventStoreCatchUpSubscription, ResolvedEvent> eventAppeared, 
             Action<EventStoreCatchUpSubscription> liveProcessingStarted = null, 
             Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null, 
             UserCredentials userCredentials = null);

        Task ConnectAsync();
        void Close();
        EventHandler<ClientConnectionEventArgs> Connected { get; set; }
        /// <summary>
        /// Asynchronously subscribes to all events in Event Store. New
        /// events written to the stream while the subscription is active
        /// will be pushed to the client.
        /// </summary>
        /// <param name="resolveLinkTos">Whether to resolve Link events automatically.</param>
        /// <param name="eventAppeared">A Task invoked and awaited when a new event is received over the subscription.</param>
        /// <param name="subscriptionDropped">An action invoked if the subscription is dropped.</param>
        /// <param name="userCredentials">User credentials to use for the operation.</param>
        /// <returns>A <see cref="T:System.Threading.Tasks.Task`1" /> representing the subscription.</returns>
        Task<EventStoreSubscription> SubscribeToAllAsync(bool resolveLinkTos, Action<EventStoreSubscription, ResolvedEvent> eventAppeared, Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null, UserCredentials userCredentials = null);

        /// <summary>
        /// Starts an asynchronous transaction in Event Store on a given stream.
        /// </summary>
        /// <remarks>
        /// A <see cref="T:EventStore.ClientAPI.EventStoreTransaction" /> allows the calling of multiple writes with multiple
        /// round trips over long periods of time between the caller and Event Store. This method
        /// is only available through the TCP interface and no equivalent exists for the RESTful interface.
        /// </remarks>
        /// <param name="stream">The stream to start a transaction on.</param>
        /// <param name="expectedVersion">The expected version of the stream at the time of starting the transaction.</param>
        /// <param name="userCredentials">The optional user credentials to perform operation with.</param>
        /// <returns>A <see cref="T:System.Threading.Tasks.Task`1" /> representing a multi-request transaction.</returns>
        Task<EventStoreTransaction> StartTransactionAsync(string stream, long expectedVersion, UserCredentials userCredentials = null);

        Task<DeleteResult> DeleteStreamAsync(StreamName stream, int expectedVersion);
    }
}