using System;
using ReactiveDomain.Util;

namespace ReactiveDomain
{
    public interface IStreamStoreConnection : IDisposable
    {

        /// <summary>
        /// Gets the name of this connection. A connection name is useful for dT:ReactiveDomain.IStreamStoreConnectionisambiguation
        /// in log files.
        /// </summary>
        string ConnectionName { get; }

        /// <summary>
        /// Connects the <see cref="T:ReactiveDomain.IStreamStoreConnection" /> asynchronously to a destination.
        /// </summary>
        void Connect();

        /// <summary>
        /// Closes this <see cref="T:ReactiveDomain.IStreamStoreConnection" />.
        /// </summary>

        void Close();
        
        /// <summary>
        /// Fired when an <see cref="T:ReactiveDomain.IStreamStoreConnection" /> connects to an Event Store server.
        /// </summary>
        event EventHandler<ClientConnectionEventArgs> Connected;

        /// <summary>
        /// Fired when an <see cref="T:ReactiveDomain.IStreamStoreConnection" /> disconnects from an Event Store server.
        /// </summary>
        event EventHandler<ClientConnectionEventArgs> Disconnected;

        /// <summary>Appends events asynchronously to a stream.</summary>
        /// <remarks>
        /// When appending events to a stream the <see cref="T:ReactiveDomain.ExpectedVersion" /> choice can
        /// make a large difference in the observed behavior. For example, if no stream exists
        /// and ExpectedVersion.Any is used, a new stream will be implicitly created when appending.
        /// There are also differences in idempotency between different types of calls.
        /// If you specify an ExpectedVersion aside from ExpectedVersion.Any, Event Store
        /// will give you an idempotency guarantee. If using ExpectedVersion.Any, Event Store
        /// will do its best to provide idempotency but does not guarantee idempotency.
        /// </remarks>
        /// <param name="stream">The name of the stream to append events to.</param>
        /// <param name="expectedVersion">The <see cref="T:ReactiveDomain.ExpectedVersion" /> of the stream to append to.</param>
        /// /// <param name="credentials">The user credentials</param>
        /// <param name="events">The events to append to the stream.</param>
        /// <returns>A WriteResult containing the results of the write operation.</returns>
        WriteResult AppendToStream(string stream, long expectedVersion, UserCredentials credentials = null, params EventData[] events);

        /// <summary>
        /// Reads count events from an event stream forwards (e.g. oldest to newest) starting from position start.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="start">The starting point to read from.</param>
        /// <param name="count">The count of items to read.</param>
        /// <param name="credentials">The user credentials</param>
        /// <returns>A StreamEventsSlice containing the results of the read operation.</returns>
        StreamEventsSlice ReadStreamForward(string stream, long start, long count, UserCredentials credentials = null);

        /// <summary>
        /// Reads count events from an event stream forwards (e.g. oldest to newest) starting from position start.
        /// </summary>
        /// <param name="stream">The stream to read from.</param>
        /// <param name="start">The starting point to read from.</param>
        /// <param name="count">The count of items to read.</param>
        /// <param name="credentials">The user credentials</param>
        /// <returns>A StreamEventsSlice containing the results of the read operation.</returns>
        StreamEventsSlice ReadStreamBackward(string stream, long start, long count, UserCredentials credentials = null);

        /// <summary>
        /// Asynchronously subscribes to a single event stream. New events
        /// written to the stream while the subscription is active will be
        /// pushed to the client.
        /// </summary>
        /// <param name="stream">The stream to subscribe to.</param>
        /// <param name="eventAppeared">A Task invoked and awaited when a new event is received over the subscription.</param>
        /// <param name="subscriptionDropped">An action invoked if the subscription is dropped.</param>
        /// <param name="userCredentials">User credentials to use for the operation.</param>
        /// <returns>An IDisposable that can be used to dispose the subscription.</returns>
        IDisposable SubscribeToStream(
            string stream,
            Action<RecordedEvent> eventAppeared,
            Action<SubscriptionDropReason, Exception> subscriptionDropped = null,
            UserCredentials userCredentials = null);

        /// <summary>
        /// Subscribes to a single event stream. Existing events from
        /// lastCheckpoint onwards are read from the stream
        /// and presented to the user of <see cref="T:ReactiveDomain.EventStoreCatchUpSubscription" />
        /// as if they had been pushed.
        /// 
        /// Once the end of the stream is read the subscription is
        /// transparently (to the user) switched to push new events as
        /// they are written.
        /// 
        /// The action liveProcessingStarted is called when the
        /// <see cref="T:ReactiveDomain.EventStoreCatchUpSubscription" /> switches from the reading
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
        /// <param name="settings">The <see cref="T:ReactiveDomain.CatchUpSubscriptionSettings" /> for the subscription.</param>
        /// <returns>An IDisposable that can be used to close the subscription.</returns>
        IDisposable SubscribeToStreamFrom(
                 string stream,
                 long? lastCheckpoint,
                 CatchUpSubscriptionSettings settings,
                 Action<RecordedEvent> eventAppeared,
                 Action<Unit> liveProcessingStarted = null,
                 Action<SubscriptionDropReason, Exception> subscriptionDropped = null,
                 UserCredentials userCredentials = null);

        /// <summary>
        /// Asynchronously subscribes to all events. New
        /// events written to the stream while the subscription is active
        /// will be pushed to the client.
        /// </summary>
        /// <param name="eventAppeared">A Task invoked and awaited when a new event is received over the subscription.</param>
        /// <param name="subscriptionDropped">An action invoked if the subscription is dropped.</param>
        /// <param name="userCredentials">User credentials to use for the operation.</param>
        /// <param name="resolveLinkTos">If true, resolve link events.</param>
        /// <returns>An IDisposable that can be used to close the subscription.</returns>
        IDisposable SubscribeToAll(
            Action<RecordedEvent> eventAppeared,
            Action<SubscriptionDropReason, Exception> subscriptionDropped = null,
            UserCredentials userCredentials = null,
            bool resolveLinkTos = true);

        IDisposable SubscribeToAllFrom(
            Position from,
            Action<RecordedEvent> eventAppeared,
            CatchUpSubscriptionSettings settings = null,
            Action liveProcessingStarted = null,
            Action<SubscriptionDropReason, Exception> subscriptionDropped = null,
            UserCredentials userCredentials = null,
            bool resolveLinkTos = true);

        void DeleteStream(string stream, long expectedVersion, UserCredentials credentials = null);

        void HardDeleteStream(string stream, long expectedVersion, UserCredentials credentials = null);
    }
}