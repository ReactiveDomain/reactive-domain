namespace ReactiveDomain
{
	public interface ISnapshotDeserializer
	{
		T Deserialize<T>(SerializedMessage message) where T:Snapshot;
	}
}