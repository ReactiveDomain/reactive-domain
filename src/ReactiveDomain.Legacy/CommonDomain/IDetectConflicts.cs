using System.Collections.Generic;

namespace ReactiveDomain.Legacy.CommonDomain
{
    public interface IDetectConflicts
	{
		void Register<TUncommitted, TCommitted>(ConflictDelegate handler)
			where TUncommitted : class
			where TCommitted : class;

		bool ConflictsWith(IEnumerable<object> uncommittedEvents, IEnumerable<object> committedEvents);
	}

	public delegate bool ConflictDelegate(object uncommitted, object committed);
}