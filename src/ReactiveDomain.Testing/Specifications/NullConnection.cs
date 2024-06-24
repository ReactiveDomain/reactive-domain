using ReactiveDomain.Util;
using System;

namespace ReactiveDomain.Testing
{
    /// <summary>
    /// An empty connection that implements <see cref="IStreamStoreConnection"/>.
    /// </summary>
    public class NullConnection : IStreamStoreConnection
    {
        /// <summary>
        /// The name of the connection.
        /// </summary>
        public string ConnectionName => "NullConnection";

        /// <summary>
        /// Drops the events and returns a write result at the default version.
        /// </summary>
        /// <param name="stream">This parameter is ignored.</param>
        /// <param name="expectedVersion">This parameter is ignored.</param>
        /// <param name="credentials">This parameter is ignored.</param>
        /// <param name="events">This parameter is ignored.</param>
        /// <returns>A <see cref="WriteResult"/> at the default version.</returns>
        public WriteResult AppendToStream(string stream, long expectedVersion, UserCredentials credentials = null, params EventData[] events)
        {
            return new WriteResult(0);
        }

        /// <summary>
        /// Does nothing. Required for implementation of <see cref="IStreamStoreConnection"/>.
        /// </summary>
        public void Close() { }

        /// <summary>
        /// Does nothing. Required for implementation of <see cref="IStreamStoreConnection"/>.
        /// </summary>
        public void Connect() { }

        /// <summary>
        /// Does nothing. Required for implementation of <see cref="IStreamStoreConnection"/>.
        /// </summary>
        /// <param name="stream">This parameter is ignored.</param>
        /// <param name="expectedVersion">This parameter is ignored.</param>
        /// <param name="credentials">This parameter is ignored.</param>
        public void DeleteStream(string stream, long expectedVersion, UserCredentials credentials = null)
        {
        }

        /// <summary>
        /// Cleans up resources.
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// Does nothing. Required for implementation of <see cref="IStreamStoreConnection"/>.
        /// </summary>
        /// <param name="stream">This parameter is ignored.</param>
        /// <param name="expectedVersion">This parameter is ignored.</param>
        /// <param name="credentials">This parameter is ignored.</param>
        public void HardDeleteStream(string stream, long expectedVersion, UserCredentials credentials = null)
        {
        }

        /// <summary>
        /// Gets an empty stream slice.
        /// </summary>
        /// <param name="stream">This parameter is ignored.</param>
        /// <param name="start">This parameter is ignored.</param>
        /// <param name="count">This parameter is ignored.</param>
        /// <param name="credentials">This parameter is ignored.</param>
        /// <returns>An empty <see cref="StreamEventsSlice"/>.</returns>
        public StreamEventsSlice ReadStreamBackward(string stream, long start, long count, UserCredentials credentials = null)
        {
            return new StreamEventsSlice(stream, 0, ReadDirection.Backward, Array.Empty<RecordedEvent>(), 0, 0, true);
        }

        /// <summary>
        /// Gets an empty stream slice.
        /// </summary>
        /// <param name="stream">This parameter is ignored.</param>
        /// <param name="start">This parameter is ignored.</param>
        /// <param name="count">This parameter is ignored.</param>
        /// <param name="credentials">This parameter is ignored.</param>
        /// <returns>An empty <see cref="StreamEventsSlice"/>.</returns>
        public StreamEventsSlice ReadStreamForward(string stream, long start, long count, UserCredentials credentials = null)
        {
            return new StreamEventsSlice(stream, 0, ReadDirection.Forward, Array.Empty<RecordedEvent>(), 0, 0, true);
        }

        /// <summary>
        /// Does nothing. Required for implementation of <see cref="IStreamStoreConnection"/>.
        /// </summary>
        /// <param name="eventAppeared">This parameter is ignored.</param>
        /// <param name="subscriptionDropped">This parameter is ignored.</param>
        /// <param name="credentials">This parameter is ignored.</param>
        /// <param name="resolveLinkTos">This parameter is ignored.</param>
        /// <returns>This connection.</returns>
        public IDisposable SubscribeToAll(Action<RecordedEvent> eventAppeared, Action<SubscriptionDropReason, Exception> subscriptionDropped = null, UserCredentials credentials = null, bool resolveLinkTos = true)
        {
            return this;
        }

        /// <summary>
        /// Does nothing. Required for implementation of <see cref="IStreamStoreConnection"/>.
        /// </summary>
        /// <param name="from">This parameter is ignored.</param>
        /// <param name="eventAppeared">This parameter is ignored.</param>
        /// <param name="settings">This parameter is ignored.</param>
        /// <param name="liveProcessingStarted">This parameter is ignored.</param>
        /// <param name="subscriptionDropped">This parameter is ignored.</param>
        /// <param name="credentials">This parameter is ignored.</param>
        /// <param name="resolveLinkTos">This parameter is ignored.</param>
        /// <returns>This connection.</returns>
        public IDisposable SubscribeToAllFrom(Position from, Action<RecordedEvent> eventAppeared, CatchUpSubscriptionSettings settings = null, Action liveProcessingStarted = null, Action<SubscriptionDropReason, Exception> subscriptionDropped = null, UserCredentials credentials = null, bool resolveLinkTos = true)
        {
            return this;
        }

        /// <summary>
        /// Does nothing. Required for implementation of <see cref="IStreamStoreConnection"/>.
        /// </summary>
        /// <param name="stream">This parameter is ignored.</param>
        /// <param name="eventAppeared">This parameter is ignored.</param>
        /// <param name="subscriptionDropped">This parameter is ignored.</param>
        /// <param name="credentials">This parameter is ignored.</param>
        /// <returns>This connection.</returns>
        public IDisposable SubscribeToStream(string stream, Action<RecordedEvent> eventAppeared, Action<SubscriptionDropReason, Exception> subscriptionDropped = null, UserCredentials credentials = null)
        {
            return this;
        }

        /// <summary>
        /// Does nothing. Required for implementation of <see cref="IStreamStoreConnection"/>.
        /// </summary>
        /// <param name="stream">This parameter is ignored.</param>
        /// <param name="lastCheckpoint">This parameter is ignored.</param>
        /// <param name="settings">This parameter is ignored.</param>
        /// <param name="eventAppeared">This parameter is ignored.</param>
        /// <param name="liveProcessingStarted">This parameter is ignored.</param>
        /// <param name="subscriptionDropped">This parameter is ignored.</param>
        /// <param name="credentials"></param>
        /// <returns>This connection.</returns>
        public IDisposable SubscribeToStreamFrom(string stream, long? lastCheckpoint, CatchUpSubscriptionSettings settings, Action<RecordedEvent> eventAppeared, Action<Unit> liveProcessingStarted = null, Action<SubscriptionDropReason, Exception> subscriptionDropped = null, UserCredentials credentials = null)
        {
            return this;
        }
    }
}
