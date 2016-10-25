using System;

namespace ReactiveDomain.Domain
{
    public interface IConstructAggregates
	{
		IAggregate Build(Type type, Guid id, IMemento snapshot);
	}
}