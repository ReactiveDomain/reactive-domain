using System;
using System.Runtime.Serialization;

namespace ReactiveDomain {
    /// <summary>
    /// Base type for exceptions thrown by an <see cref="T:ReactiveDomain.IStreamStoreConnection" />,
    /// thrown in circumstances which do not have a specific derived exception.
    /// </summary>
    public class StreamStoreConnectionException : Exception {
        public StreamStoreConnectionException(
                string message = null, Exception innerException = null)
            : base(message, innerException) { }
        protected StreamStoreConnectionException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
    public class StreamStoreNotAvailableException : Exception {

        public StreamStoreNotAvailableException(
                    string message = null,
                    Exception innerException = null)
            : base(message, innerException) { }
        protected StreamStoreNotAvailableException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
    /// <summary>
    /// Exception thrown if the expected version specified on an operation
    /// does not match the version of the stream when the operation was attempted.
    /// </summary>
    public class WrongExpectedVersionException : StreamStoreConnectionException {
        public WrongExpectedVersionException(string message, Exception innerException = null)
            : base(message, innerException) { }
        public WrongExpectedVersionException(string stream, int position, int expected, Exception innerException = null)
            : base($"The stream {stream} was found at {position}, expected position was {expected}.", innerException) { }
        protected WrongExpectedVersionException(SerializationInfo info, StreamingContext context)
            : base(info, context) { }
    }
    /// <summary>
    /// Exception thrown if the specified stream was not found.
    /// </summary>
    public class StreamNotFoundException : StreamStoreConnectionException {
        public StreamNotFoundException(string stream, Exception innerException = null)
            : base($"The stream {stream} was not found.", innerException) {
            Stream = new StreamName(stream);
        }

        public StreamName Stream { get; }
    }
    /// <summary>
    /// Exception thrown if the specified stream has been deleted.
    /// </summary>
    public class StreamDeletedException : Exception {
        public StreamDeletedException(StreamName stream, Exception innerException = null)
            : base($"The stream {stream} was deleted.", innerException) {
            Stream = stream;
        }
        public StreamName Stream { get; }
    }
}