using System;

namespace ReactiveDomain.Foundation
{
    public interface IRepository
	{
        bool TryGetById<TAggregate>(Guid id, out TAggregate aggregate) where TAggregate : class, IEventSource;
        bool TryGetById<TAggregate>(Guid id, int version, out TAggregate aggregate) where TAggregate : class, IEventSource;
        TAggregate GetById<TAggregate>(Guid id) where TAggregate : class, IEventSource;
		TAggregate GetById<TAggregate>(Guid id, int version) where TAggregate : class, IEventSource;
		void Save(IEventSource aggregate);
	}
}