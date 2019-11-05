using System;

namespace ReactiveDomain.Foundation.StreamStore {
    /// <summary>
    /// While it might seem more natural to save and restore the event set behind the aggregate,
    /// this cache stores only the collapsed state in the aggregate  
    /// </summary>
    public interface IAggregateCache: IDisposable
    {
        bool GetById<TAggregate>(Guid id, out TAggregate aggregate) where TAggregate : class, IEventSource;
        bool Save(IEventSource aggregate);
        bool Remove(Guid id);
        void Clear();
    }
}