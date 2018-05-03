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
        
        public bool TryGetById<TAggregate>(Guid id, out TAggregate aggregate, CorrelatedMessage source) where TAggregate : CorrelatedEDSM, IEventSource {
            return TryGetById(id, int.MaxValue, out aggregate, source);
        }

        public TAggregate GetById<TAggregate>(Guid id, CorrelatedMessage source) where TAggregate : CorrelatedEDSM, IEventSource {
           return GetById<TAggregate>(id, int.MaxValue, source);
        }
        
        public bool TryGetById<TAggregate>(Guid id, int version, out TAggregate aggregate, CorrelatedMessage source) where TAggregate : CorrelatedEDSM, IEventSource {
            try {
                aggregate = GetById<TAggregate>(id, version, source);
                return true;
            }
            catch (Exception) {
                aggregate = null;
                return false;
            }
        }
       
        public TAggregate GetById<TAggregate>(Guid id, int version, CorrelatedMessage source) where TAggregate : CorrelatedEDSM, IEventSource {
            var agg = _repository.GetById<TAggregate>(id, version);
            agg.ApplyNewSource(source);
            return agg;
        }
       

        public void Save(IEventSource aggregate) {
            _repository.Save(aggregate);
        }
    }
}
