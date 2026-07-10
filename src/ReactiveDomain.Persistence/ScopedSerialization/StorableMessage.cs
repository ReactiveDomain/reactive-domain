namespace ReactiveDomain;

public readonly struct StorableMessage(Guid id, string name, byte[] data, byte[] metadata) {
	public readonly Guid Id = id;
	public readonly string Name = name;
	public readonly byte[] Data = data;
	public readonly byte[] Metadata = metadata;
}
