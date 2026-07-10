namespace ReactiveDomain;

/// <summary>Represents a previously written event</summary>
public class RecordedEvent(
	string eventStreamId,
	Guid eventId,
	long eventNumber,
	string eventType,
	byte[] data,
	byte[] metadata,
	bool isJson,
	DateTime created,
	long createdEpoch)
	: IEventData {
	/// <summary>The Event Stream that this event belongs to</summary>
	public readonly string EventStreamId = eventStreamId;

	/// <summary>The Unique Identifier representing this event</summary>
	public Guid EventId { get; } = eventId;

	/// <summary>The number of this event in the stream</summary>
	public readonly long EventNumber = eventNumber;
	/// <summary>The type of event this is</summary>
	public string EventType { get; } = eventType;

	/// <summary>A byte array representing the data of this event</summary>
	public byte[] Data { get; } = data;

	/// <summary>
	/// A byte array representing the metadata associated with this event
	/// </summary>
	public byte[] Metadata { get; } = metadata;

	/// <summary>
	/// Indicates whether the content is internally marked as json
	/// </summary>
	public bool IsJson { get; } = isJson;

	/// <summary>
	/// A datetime representing when this event was created in the system
	/// </summary>
	public readonly DateTime Created = created;
	/// <summary>
	/// A long representing the milliseconds since the epoch when the was created in the system
	/// </summary>
	public readonly long CreatedEpoch = createdEpoch;
}
