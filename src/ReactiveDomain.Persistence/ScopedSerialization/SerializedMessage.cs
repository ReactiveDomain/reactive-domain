namespace ReactiveDomain;

public readonly struct SerializedMessage : IEquatable<SerializedMessage> {
	public readonly Guid Id;
	public readonly string Name;
	public readonly string StreamName;
	public readonly long StreamRevision;
	public readonly byte[] Data;
	public readonly byte[]? Metadata;

	public static readonly SerializedMessage None = new();

	public SerializedMessage(Guid id, string name, string streamName, long streamRevision, byte[] data, byte[]? metadata) {
		Id = id;
		Name = name;
		StreamName = streamName;
		StreamRevision = streamRevision;
		Data = data;
		Metadata = metadata;
	}

	public bool Equals(SerializedMessage other) {
		return string.Equals(Name, other.Name) && string.Equals(StreamName, other.StreamName) && StreamRevision == other.StreamRevision;
	}

	public override bool Equals(object? obj) {
		if (ReferenceEquals(null, obj))
			return false;
		return obj is SerializedMessage serializedMessage && Equals(serializedMessage);
	}

	public override int GetHashCode() {
		return HashCode.Combine(Name, StreamName, StreamRevision);
	}

	public static bool operator ==(SerializedMessage left, SerializedMessage right) {
		return left.Equals(right);
	}

	public static bool operator !=(SerializedMessage left, SerializedMessage right) {
		return !left.Equals(right);
	}
}
