using System;

namespace ReactiveDomain
{
    public abstract class StreamSubscription : IDisposable
    {
        /// <summary>
        /// The last commit position seen on the subscription (if this is
        /// a subscription to all events).
        /// </summary>
        public readonly long LastCommitPosition;
        /// <summary>
        /// The last event number seen on the subscription (if this is a
        /// subscription to a single stream).
        /// </summary>
        public readonly long? LastEventNumber;

        /// <summary>True if this subscription is to all streams.</summary>
        public bool IsSubscribedToAll => StreamId == string.Empty;

        /// <summary>
        /// The name of the stream to which the subscription is subscribed.
        /// </summary>
        public string StreamId { get; }

        internal StreamSubscription(string streamId, long lastCommitPosition, long? lastEventNumber)
        {
            StreamId = streamId;
            LastCommitPosition = lastCommitPosition;
            LastEventNumber = lastEventNumber;
        }

        /// <summary>Unsubscribes from the stream.</summary>
        public void Dispose()
        {
            Unsubscribe();
        }

        /// <summary>Unsubscribes from the stream.</summary>
        public void Close()
        {
            Unsubscribe();
        }

        /// <summary>Unsubscribes from the stream</summary>
        public abstract void Unsubscribe();
    }
}
