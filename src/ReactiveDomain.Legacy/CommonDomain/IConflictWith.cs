namespace ReactiveDomain.Legacy.CommonDomain
{
	public interface IConflictWith
	{
		bool ConflictsWith(object uncommitted, object committed);
	}
}