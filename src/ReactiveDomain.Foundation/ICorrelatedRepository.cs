using System;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Foundation {
    public interface ICorrelatedRepository
    {
        bool TryGetById<TAggregate>(Guid id, out TAggregate aggregate, CorrelatedMessage source) where TAggregate : CorrelatedEDSM, IEventSource;
        bool TryGetById<TAggregate>(Guid id, int version, out TAggregate aggregate, CorrelatedMessage source) where TAggregate : CorrelatedEDSM, IEventSource;
        TAggregate GetById<TAggregate>(Guid id, CorrelatedMessage source) where TAggregate : CorrelatedEDSM, IEventSource;
        TAggregate GetById<TAggregate>(Guid id, int version, CorrelatedMessage source) where TAggregate : CorrelatedEDSM, IEventSource;
        void Save(IEventSource aggregate);
    }
}