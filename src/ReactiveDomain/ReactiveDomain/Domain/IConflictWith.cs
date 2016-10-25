namespace ReactiveDomain.Domain
{
	public interface IConflictWith
	{
		bool ConflictsWith(object uncommitted, object committed);
	}
}