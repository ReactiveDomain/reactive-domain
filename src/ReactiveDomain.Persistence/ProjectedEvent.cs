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
	long createdEpoch,
	Position? position = null)
	: RecordedEvent(eventStreamId,
		eventId,
		eventNumber,
		eventType,
		data,
		metadata,
		isJson,
		created,
		createdEpoch,
		position) {
	public string ProjectedStream = projectedStream;
	public long OriginalEventNumber = originalEventNumber;
}
