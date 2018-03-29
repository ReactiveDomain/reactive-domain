using System;
using System.Runtime.Serialization;


namespace ReactiveDomain
{
    /// <summary>
    /// Exception thrown if the expected version specified on an operation
    /// does not match the version of the stream when the operation was attempted.
    /// </summary>
    public class WrongExpectedVersionException : EventStoreConnectionException
    {
        /// <summary>
        /// Constructs a new instance of <see cref="T:EventStore.ClientAPI.Exceptions.WrongExpectedVersionException" />.
        /// </summary>
        public WrongExpectedVersionException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Constructs a new instance of <see cref="T:EventStore.ClientAPI.Exceptions.WrongExpectedVersionException" />.
        /// </summary>
        public WrongExpectedVersionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        /// <summary>
        /// Constructs a new instance of <see cref="T:EventStore.ClientAPI.Exceptions.WrongExpectedVersionException" />.
        /// </summary>
        protected WrongExpectedVersionException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
