using System;

namespace ReactiveDomain
{
    public class StreamNotFoundException : Exception
    {
        public StreamNotFoundException(StreamName stream)
            : base($"The stream {stream} was not found.")
        {
            Stream = stream;
        }

        public StreamName Stream { get; }
    }
}