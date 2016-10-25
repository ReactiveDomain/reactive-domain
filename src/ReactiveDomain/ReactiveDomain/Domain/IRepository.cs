using System;
using System.Collections.Generic;
using ReactiveDomain.EventStore;

namespace ReactiveDomain.Domain
{
    public interface IRepository
	{
        bool TryGetById<TAggregate>(Guid id, out TAggregate aggregate) where TAggregate : class, IAggregate;
        bool TryGetById<TAggregate>(Guid id, int version, out TAggregate aggregate) where TAggregate : class, IAggregate;
        TAggregate GetById<TAggregate>(Guid id) where TAggregate : class, IAggregate;
		TAggregate GetById<TAggregate>(Guid id, int version) where TAggregate : class, IAggregate;
		void Save(IAggregate aggregate, Guid commitId, Action<IDictionary<string, object>> updateHeaders);
        IListener GetListener(string name, bool sync = false);
	}
}