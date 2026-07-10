namespace ReactiveDomain;

public class StreamStoreReadResult(IReadOnlyList<SerializedMessage> messages, bool isEndOfStream) {
	public IReadOnlyList<SerializedMessage> Messages { get; } = messages;
	public bool IsEndOfStream { get; } = isEndOfStream;
}
