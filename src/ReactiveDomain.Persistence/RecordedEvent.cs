using System;
namespace ReactiveDomain
{
    /// <summary>Represents a previously written event</summary>
    public class RecordedEvent:IEventData
    {
        /// <summary>The Event Stream that this event belongs to</summary>
        public readonly string EventStreamId;

        /// <summary>The Unique Identifier representing this event</summary>
        public Guid EventId { get; }

        /// <summary>The number of this event in the stream</summary>
        public readonly long EventNumber;
        /// <summary>The type of event this is</summary>
        public string EventType { get; }
        /// <summary>A byte array representing the data of this event</summary>
        public byte[] Data { get; }
        /// <summary>
        /// A byte array representing the metadata associated with this event
        /// </summary>
        public byte[] Metadata { get; }
        /// <summary>
        /// Indicates whether the content is internally marked as json
        /// </summary>
        public bool IsJson { get; }
        /// <summary>
        /// A datetime representing when this event was created in the system
        /// </summary>
        public readonly DateTime Created;
        /// <summary>
        /// A long representing the milliseconds since the epoch when the was created in the system
        /// </summary>
        public readonly long CreatedEpoch;

        public RecordedEvent(
                    string eventStreamId, 
                    Guid eventId, 
                    long eventNumber, 
                    string eventType, 
                    byte[] data, 
                    byte[] metadata, 
                    bool isJson, 
                    DateTime created, 
                    long createdEpoch) {
            EventStreamId = eventStreamId;
            EventId = eventId;
            EventNumber = eventNumber;
            EventType = eventType;
            Data = data;
            Metadata = metadata;
            IsJson = isJson;
            Created = created;
            CreatedEpoch = createdEpoch;
        }

     
    }
}
