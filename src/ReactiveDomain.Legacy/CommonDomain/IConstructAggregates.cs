using System;

namespace ReactiveDomain.Legacy.CommonDomain
{
    public interface IConstructAggregates
	{
		IAggregate Build(Type type, Guid id, IMemento snapshot);
	}
}