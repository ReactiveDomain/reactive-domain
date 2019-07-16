using System;
using System.Collections.Generic;

namespace ReactiveDomain.Foundation.StreamStore {
    /// <summary>
    /// This implementation assumes that the caller is the sole owner for updating the cached
    /// aggregate and wrong version errors should not occur in normal practice
    ///
    /// Therefore the aggregate is cached on first save and the store is not checked for updates upon retrieving
    ///
    /// Also retrieving an aggregate at anything other than latest is disabled
    ///
    /// Cache management (e.g. eviction) is the responsibility of the caller
    ///
    /// Save failures will clear the aggregate from the cache, allowing getting an updated version and
    /// reapplying the commands 
    /// </summary>
    
    public class OptimisticCacheRepo {
        private readonly IRepository _repo;
        private readonly Dictionary<Guid, IEventSource> _knownAggregates = new Dictionary<Guid, IEventSource>();
        public OptimisticCacheRepo(IRepository target) {
            _repo = target;
        }

        public bool TryGetById<TAggregate>(Guid id, out IEventSource aggregate) {
            return _knownAggregates.TryGetValue(id, out aggregate) ||
                    _repo.TryGetById(id, out aggregate);
        }

        public TAggregate GetById<TAggregate>(Guid id) where TAggregate : class, IEventSource  {
            _knownAggregates.TryGetValue(id, out var aggregate);
            return aggregate as TAggregate ?? _repo.GetById<TAggregate>(id);
        }

        public bool TrySave(IEventSource aggregate, out Exception exception) {
            try {
                _repo.Save(aggregate);
                if (!_knownAggregates.ContainsKey(aggregate.Id)) {
                    _knownAggregates.Add(aggregate.Id, aggregate);
                }
                exception = null;
                return true;
            }
            catch (Exception ex) {
                _knownAggregates.Remove(aggregate.Id);
                exception = ex;
                return false;
            }
        }
        public bool ClearCache(Guid id) {
            return _knownAggregates.Remove(id);
        }
        public void ClearCache() {
            _knownAggregates.Clear();
        }

    }
}
