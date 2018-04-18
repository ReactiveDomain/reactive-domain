using System;
using ReactiveDomain.Util;

namespace ReactiveDomain {
    /// <summary>Represents an event to be written. Mapped from EventStore.ClientAPI</summary>
    public interface IEventData {
        /// <summary>
        /// The ID of the event, used as part of the idempotent write check.
        /// </summary>
        Guid EventId { get; }
        /// <summary>
        /// The name of the event type. It is strongly recommended that these
        /// use lowerCamelCase if projections are to be used.
        /// </summary>
        string EventType { get; }
        /// <summary>
        /// Flag indicating whether the data and metadata are JSON.
        /// </summary>
        bool IsJson { get; }
        /// <summary>The raw bytes of the event data.</summary>
        byte[] Data { get; }
        /// <summary>The raw bytes of the event metadata.</summary>
        byte[] Metadata { get; }
    }

    /// <summary>Represents an event to be written. Mapped from EventStore.ClientAPI</summary>
    public sealed class EventData : IEventData {
        /// <summary>
        /// The ID of the event, used as part of the idempotent write check.
        /// </summary>
        public Guid EventId { get; }
        /// <summary>
        /// The name of the event type. It is strongly recommended that these
        /// use lowerCamelCase if projections are to be used.
        /// </summary>
        public string EventType { get; }
        /// <summary>
        /// Flag indicating whether the data and metadata are JSON.
        /// </summary>
        public bool IsJson { get; }
        /// <summary>The raw bytes of the event data.</summary>
        public byte[] Data { get; }
        /// <summary>The raw bytes of the event metadata.</summary>
        public byte[] Metadata { get; }

        /// <summary>
        /// Constructs a new <see cref="T:EventStore.ClientAPI.EventData" />.
        /// </summary>
        /// <param name="eventId">The ID of the event, used as part of the idempotent write check.</param>
        /// <param name="type">The name of the event type. It is strongly recommended that these
        /// use lowerCamelCase if projections are to be used.</param>
        /// <param name="isJson">Flag indicating whether the data and metadata are JSON.</param>
        /// <param name="data">The raw bytes of the event data.</param>
        /// <param name="metadata">The raw bytes of the event metadata.</param>
        public EventData(Guid eventId, string type, bool isJson, byte[] data, byte[] metadata) {
            if (string.IsNullOrWhiteSpace(type))
                throw new ArgumentNullException(nameof(type), "Type cannot be null, empty or whitespace");
            Ensure.NotEmptyGuid(eventId, nameof(eventId));
            EventId = eventId;
            EventType = type;
            IsJson = isJson;
            Data = data ?? new byte[0];
            Metadata = metadata ?? new byte[0];
        }
    }
}
