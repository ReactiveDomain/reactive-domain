using System;

namespace ReactiveDomain
{
    public class EventSourceChangeset
    {
        public StreamName Stream { get; }
        public long ExpectedVersion { get; }
        public Guid Causation { get; }
        public Guid Correlation { get; }
        public Metadata Metadata { get; }
        public object[] Events { get; }

        public EventSourceChangeset(
            StreamName stream, 
            long expectedVersion, 
            Guid causation, 
            Guid correlation, 
            Metadata metadata, 
            object[] events)
        {
            Stream = stream;
            ExpectedVersion = expectedVersion;
            Causation = causation;
            Correlation = correlation;
            Metadata = metadata;
            Events = events;
        }
    }
}