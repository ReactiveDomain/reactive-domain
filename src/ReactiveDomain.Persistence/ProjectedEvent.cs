namespace ReactiveDomain;

public class ProjectedEvent(
	string projectedStream,
	long originalEventNumber,
	string eventStreamId,
	Guid eventId,
	long eventNumber,
	string eventType,
	byte[] data,
	byte[] metadata,
	bool isJson,
	DateTime created,
	long createdEpoch)
	: RecordedEvent(eventStreamId,
		eventId,
		eventNumber,
		eventType,
		data,
		metadata,
		isJson,
		created,
		createdEpoch) {
	public string ProjectedStream = projectedStream;
	public long OriginalEventNumber = originalEventNumber;
}
