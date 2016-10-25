using System;

namespace ReactiveDomain.Domain
{
    public interface IRouteEvents
	{
		void Register<T>(Action<T> handler);
		void Register(IAggregate aggregate);

		void Dispatch(object eventMessage);
	}
}