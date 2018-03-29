using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactiveDomain
{
    public abstract class EventStoreSubscription : IDisposable
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
        public bool IsSubscribedToAll
        {
            get
            {
                return this.StreamId == string.Empty;
            }
        }

        /// <summary>
        /// The name of the stream to which the subscription is subscribed.
        /// </summary>
        public string StreamId { get; }

        internal EventStoreSubscription(string streamId, long lastCommitPosition, long? lastEventNumber)
        {
            this.StreamId = streamId;
            this.LastCommitPosition = lastCommitPosition;
            this.LastEventNumber = lastEventNumber;
        }

        /// <summary>Unsubscribes from the stream.</summary>
        public void Dispose()
        {
            this.Unsubscribe();
        }

        /// <summary>Unsubscribes from the stream.</summary>
        public void Close()
        {
            this.Unsubscribe();
        }

        /// <summary>Unsubscribes from the stream</summary>
        public abstract void Unsubscribe();
    }
}
