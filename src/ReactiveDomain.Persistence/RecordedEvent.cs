using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ReactiveDomain
{
    /// <summary>Represents a previously written event</summary>
    public class RecordedEvent
    {
        /// <summary>The Event Stream that this event belongs to</summary>
        public readonly string EventStreamId;
        /// <summary>The Unique Identifier representing this event</summary>
        public readonly Guid EventId;
        /// <summary>The number of this event in the stream</summary>
        public readonly long EventNumber;
        /// <summary>The type of event this is</summary>
        public readonly string EventType;
        /// <summary>A byte array representing the data of this event</summary>
        public readonly byte[] Data;
        /// <summary>
        /// A byte array representing the metadata associated with this event
        /// </summary>
        public readonly byte[] Metadata;
        /// <summary>
        /// Indicates whether the content is internally marked as json
        /// </summary>
        public readonly bool IsJson;
        /// <summary>
        /// A datetime representing when this event was created in the system
        /// </summary>
        public DateTime Created;
        /// <summary>
        /// A long representing the milliseconds since the epoch when the was created in the system
        /// </summary>
        public long CreatedEpoch;

        //internal RecordedEvent(EventRecord systemRecord)
        //{
        //    this.EventStreamId = systemRecord.EventStreamId;
        //    this.EventId = new Guid(systemRecord.EventId);
        //    this.EventNumber = systemRecord.EventNumber;
        //    this.EventType = systemRecord.EventType;
        //    if (systemRecord.Created.HasValue)
        //        this.Created = DateTime.FromBinary(systemRecord.Created.Value);
        //    if (systemRecord.CreatedEpoch.HasValue)
        //        this.CreatedEpoch = systemRecord.CreatedEpoch.Value;
        //    this.Data = systemRecord.Data ?? EventStore.ClientAPI.Internal.Empty.ByteArray;
        //    this.Metadata = systemRecord.Metadata ?? EventStore.ClientAPI.Internal.Empty.ByteArray;
        //    this.IsJson = systemRecord.DataContentType == 1;
        //}
    }
}
