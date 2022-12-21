namespace ReactiveDomain
{
	public interface ISnapshotSerializer
	{
		StorableMessage Serialize(Snapshot snapshot);
	}
}