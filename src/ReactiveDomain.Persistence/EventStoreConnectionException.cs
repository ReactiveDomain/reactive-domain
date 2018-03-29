using System;
using System.Runtime.Serialization;

namespace ReactiveDomain {
    /// <summary>
    /// Base type for exceptions thrown by an <see cref="T:EventStore.ClientAPI.IEventStoreConnection" />,
    /// thrown in circumstances which do not have a specific derived exception.
    /// </summary>
    public class EventStoreConnectionException : Exception
    {
        /// <summary>
        /// Constructs a new <see cref="T:EventStore.ClientAPI.Exceptions.EventStoreConnectionException" />.
        /// </summary>
        public EventStoreConnectionException()
        {
        }

        /// <summary>
        /// Constructs a new <see cref="T:EventStore.ClientAPI.Exceptions.EventStoreConnectionException" />.
        /// </summary>
        public EventStoreConnectionException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Constructs a new <see cref="T:EventStore.ClientAPI.Exceptions.EventStoreConnectionException" />.
        /// </summary>
        public EventStoreConnectionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Constructs a new <see cref="T:EventStore.ClientAPI.Exceptions.EventStoreConnectionException" />.
        /// </summary>
        protected EventStoreConnectionException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}