using System;
using System.Runtime.Serialization;

namespace ReactiveDomain {
    /// <summary>
    /// Base type for exceptions thrown by an <see cref="T:ReactiveDomain.IStreamStoreConnection" />,
    /// thrown in circumstances which do not have a specific derived exception.
    /// </summary>
    public class StreamStoreConnectionException : Exception
    {
        public StreamStoreConnectionException(){}
        
        public StreamStoreConnectionException(string message)
            : base(message){}
        public StreamStoreConnectionException(string message, Exception innerException)
            : base(message, innerException){}
        protected StreamStoreConnectionException(SerializationInfo info, StreamingContext context)
            : base(info, context){}
    }
    /// <summary>
    /// Exception thrown if the expected version specified on an operation
    /// does not match the version of the stream when the operation was attempted.
    /// </summary>
    public class WrongExpectedVersionException : StreamStoreConnectionException
    {
        public WrongExpectedVersionException(string message)
            : base(message){}
        public WrongExpectedVersionException(string message, Exception innerException)
            : base(message, innerException){}
        protected WrongExpectedVersionException(SerializationInfo info, StreamingContext context)
            : base(info, context){}
    }
    /// <summary>
    /// Exception thrown if the specified stream was not found.
    /// </summary>
    public class StreamNotFoundException : StreamStoreConnectionException
    {
        public StreamNotFoundException(StreamName stream, Exception innerException)
            : base($"The stream {stream} was not found.", innerException)
        {
            Stream = stream;
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