using System;

namespace ReactiveDomain.Foundation.StreamStore {
    /// <summary>
    /// This implementation assumes that the cached aggregate may have changes since it was last seen
    ///
    /// Therefore the aggregate is cached on first retrieve and the store is checked for updates upon
    /// each subsequent retrieval
    ///
    /// Also retrieving an aggregate at anything other than latest is disabled
    ///
    /// Cache management (e.g. eviction) is the responsibility of the caller/external owner
    ///
    /// Save failures will clear the aggregate from the cache and rethrow
    /// </summary>
    public class CachingRepository : IDisposable
    {
        private readonly IAggregateCache _cache;
        public CachingRepository(IRepository baseRepository, Func<IRepository,IAggregateCache> cacheFactory = null) {
            if (cacheFactory == null) {
                cacheFactory = repo => new ReadThroughAggregateCache(repo);
            }
            _cache = cacheFactory(baseRepository);
        }
        public bool TryGetById<TAggregate>(Guid id, out TAggregate aggregate) where TAggregate : class, IEventSource {
            return _cache.TryGetById(id, out aggregate);
        }
        public TAggregate GetById<TAggregate>(Guid id) where TAggregate : class, IEventSource {
            return _cache.GetById<TAggregate>(id);
        }
        public void Save(IEventSource aggregate) {
            _cache.Save(aggregate);
        }
        public void Delete(IEventSource aggregate) {
            _cache.Delete(aggregate);
        }
        public void HardDelete(IEventSource aggregate) {
            _cache.HardDelete(aggregate);
        }
        public bool ClearCache(Guid id) {
            return _cache.Remove(id);
        }
        public void ClearCache() {
            _cache.Clear();
        }

        protected virtual void Dispose(bool disposing) {
            if (disposing) {
                _cache.Dispose();
            }
        }
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}