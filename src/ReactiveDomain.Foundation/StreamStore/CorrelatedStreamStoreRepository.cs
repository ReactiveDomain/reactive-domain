using System;
using ReactiveDomain.Messaging;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation{
    public class CorrelatedStreamStoreRepository:ICorrelatedRepository
    {
        private readonly IRepository _repository;
        public CorrelatedStreamStoreRepository(IRepository repository) {
            _repository = repository;
        }
        public CorrelatedStreamStoreRepository(
            IStreamNameBuilder streamNameBuilder,
            IStreamStoreConnection streamStoreConnection,
            IEventSerializer eventSerializer) {
            _repository = new StreamStoreRepository(streamNameBuilder,streamStoreConnection,eventSerializer);
        }
        
        public bool TryGetById<TAggregate>(Guid id, out TAggregate aggregate, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource {
            return TryGetById(id, int.MaxValue, out aggregate, source);
        }

        public TAggregate GetById<TAggregate>(Guid id, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource {
           return GetById<TAggregate>(id, int.MaxValue, source);
        }
        
        public bool TryGetById<TAggregate>(Guid id, int version, out TAggregate aggregate, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource {
            try {
                aggregate = GetById<TAggregate>(id, version, source);
                return true;
            }
            catch (Exception) {
                aggregate = null;
                return false;
            }
        }
       
        public TAggregate GetById<TAggregate>(Guid id, int version, ICorrelatedMessage source) where TAggregate : AggregateRoot, IEventSource {
            var agg = _repository.GetById<TAggregate>(id, version);
            ((ICorrelatedEventSource)agg).Source = source;
            return agg;
        }
       

        public void Save(IEventSource aggregate) {
            _repository.Save(aggregate);
        }
    }
}
