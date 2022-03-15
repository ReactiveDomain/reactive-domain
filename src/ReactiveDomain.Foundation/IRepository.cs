using System;
namespace ReactiveDomain.Foundation
{
    public interface IRepository
	{
        bool TryGetById<TAggregate>(Guid id,  out TAggregate aggregate, int version = int.MaxValue) where TAggregate : class, IEventSource;
        TAggregate GetById<TAggregate>(Guid id, int version = int.MaxValue) where TAggregate : class, IEventSource;
		void Update<TAggregate>(ref TAggregate aggregate, int version = int.MaxValue) where TAggregate : class, IEventSource;
        void Save(IEventSource aggregate);
        void Delete(IEventSource aggregate);
        void HardDelete(IEventSource aggregate);
    }
}