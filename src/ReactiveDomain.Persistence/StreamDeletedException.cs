using System;

namespace ReactiveDomain
{
    public class StreamDeletedException : Exception
    {
        public StreamDeletedException(StreamName stream)
            : base($"The stream {stream} was deleted.")
        {
            Stream = stream;
        }

        public StreamName Stream { get; }
    }
}