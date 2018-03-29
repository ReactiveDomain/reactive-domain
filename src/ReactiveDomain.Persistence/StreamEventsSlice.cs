using System;

namespace ReactiveDomain {
    public class StreamEventsSlice {
        /// <summary>
        /// The <see cref="T:EventStore.ClientAPI.SliceReadStatus" /> representing the status of this read attempt.
        /// </summary>
        public readonly SliceReadStatus Status;
        /// <summary>The name of the stream to read.</summary>
        public readonly string Stream;
        /// <summary>
        /// The starting point (represented as a sequence number) of the read operation.
        /// </summary>
        public readonly long FromEventNumber;
        /// <summary>The direction of read request.</summary>
        public readonly ReadDirection ReadDirection;
        /// <summary>
        /// The events read represented as <see cref="T:EventStore.ClientAPI.ResolvedEvent" />.
        /// </summary>
        public readonly ResolvedEvent[] Events;
        /// <summary>The next event number that can be read.</summary>
        public readonly long NextEventNumber;
        /// <summary>The last event number in the stream.</summary>
        public readonly long LastEventNumber;
        /// <summary>
        /// A boolean representing whether or not this is the end of the stream.
        /// </summary>
        public readonly bool IsEndOfStream;

        internal StreamEventsSlice(
                    SliceReadStatus status, 
                    string stream, 
                    long fromEventNumber, 
                    ReadDirection readDirection, 
                    ResolvedEvent[] events, 
                    long nextEventNumber, 
                    long lastEventNumber, 
                    bool isEndOfStream) {
            
            if (string.IsNullOrWhiteSpace(stream)) {
                throw new ArgumentNullException(nameof(stream), "Stream cannot be null, empty or whitespace");
            }

            Status = status;
            Stream = stream;
            FromEventNumber = fromEventNumber;
            ReadDirection = readDirection;
            Events = events ??  new ResolvedEvent[0];
            
            NextEventNumber = nextEventNumber;
            LastEventNumber = lastEventNumber;
            IsEndOfStream = isEndOfStream;
        }
    }
}
