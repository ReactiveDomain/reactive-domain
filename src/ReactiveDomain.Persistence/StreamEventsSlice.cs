using System;

namespace ReactiveDomain {
    public class StreamEventsSlice {
       
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
        public readonly RecordedEvent[] Events;
        /// <summary>The next event number that can be read.</summary>
        public readonly long NextEventNumber;
        /// <summary>The last event number in the stream.</summary>
        public readonly long LastEventNumber;
        /// <summary>
        /// A boolean representing whether or not this is the end of the stream.
        /// </summary>
        public readonly bool IsEndOfStream;
        
        public StreamEventsSlice(
                    string stream, 
                    long fromEventNumber, 
                    ReadDirection readDirection, 
                    RecordedEvent[] events, 
                    long nextEventNumber, 
                    long lastEventNumber, 
                    bool isEndOfStream) {
            
            if (string.IsNullOrWhiteSpace(stream)) {
                throw new ArgumentNullException(nameof(stream), "Stream cannot be null, empty or whitespace");
            }
            Stream = stream;
            FromEventNumber = fromEventNumber;
            ReadDirection = readDirection;
            Events = events ??  new RecordedEvent[0];
            NextEventNumber = nextEventNumber;
            LastEventNumber = lastEventNumber;
            IsEndOfStream = isEndOfStream;
        }
    }

    public class StreamNotFoundSlice : StreamEventsSlice
    {
        public StreamNotFoundSlice(string stream) 
                    :base(stream, 0, 0, null, 0, 0, true){
        }
    }
    public class StreamDeletedSlice : StreamEventsSlice
    {
        public StreamDeletedSlice(string stream) 
            :base(stream, 0, 0, null, 0, 0, true){
        }
    }
}
