using System;
using ReactiveDomain.Foundation.StreamStore;
using ReactiveDomain.Messaging;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation {
    public class CorrelatedStreamStoreRepository : ICorrelatedRepository, IDisposable
    {
        private readonly IRepository _repository;
        private readonly IAggregateCache _cache;
        public CorrelatedStreamStoreRepository(
            IRepository repository,
            Func<IRepository, IAggregateCache> cacheFactory = null) {
            _repository = repository;
            if (cacheFactory != null) {
                _cache = cacheFactory(_repository);
            }
        }

        public CorrelatedStreamStoreRepository(
            IStreamNameBuilder streamNameBuilder,
            IStreamStoreConnection streamStoreConnection,
            IEventSerializer eventSerializer,
            Func<IRepository, IAggregateCache> cacheFactory = null) {
            _repository = new StreamStoreRepository(streamNameBuilder, streamStoreConnection, eventSerializer);
            if (cacheFactory != null) {
                _cache = cacheFactory(_repository);
            }
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
            TAggregate agg = _cache?.GetById<TAggregate>(id, version);

            if (agg == null || agg.Version > version) {
                agg = _repository.GetById<TAggregate>(id, version);
                if (agg != null) {
                    _cache?.Save(agg);
                }
            }

            if (agg != null) {
                ((ICorrelatedEventSource) agg).Source = source;
            }
            return agg;
        }

        public void Save(IEventSource aggregate) {
            if (_cache != null) {
                _cache.Save(aggregate);
            }
            else {
                _repository.Save(aggregate);
            }
        }

        /// <summary>
        /// Soft delete the aggregate. Its stream can be re-created by appending new events.
        /// </summary>
        /// <param name="aggregate">The aggregate to be deleted.</param>
        public void Delete(IEventSource aggregate) {
            if (_cache != null) {
                _cache.Delete(aggregate);
            }
            else {
                _repository.Delete(aggregate);
            }
        }

        /// <summary>
        /// Hard delete the aggregate. This permanently deletes the aggregate's stream.
        /// </summary>
        /// <param name="aggregate">The aggregate to be deleted.</param>
        public void HardDelete(IEventSource aggregate)
        {
            if (_cache != null) {
                _cache.HardDelete(aggregate);
            }
            else {
                _repository.HardDelete(aggregate);
            }
        }

        protected virtual void Dispose(bool disposing) {
            if (disposing) {
                _cache?.Dispose();
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
