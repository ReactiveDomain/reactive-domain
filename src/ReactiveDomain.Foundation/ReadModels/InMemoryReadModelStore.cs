using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation
{
    /// <summary>
    /// In-memory implementation of IReadModelStore for testing.
    /// Thread-safe using ConcurrentDictionary.
    /// Transaction overloads delegate to non-transactional versions (no-op for in-memory).
    /// </summary>
    /// <typeparam name="TKey">The type of the entity key.</typeparam>
    /// <typeparam name="TModel">The type of the read model entity.</typeparam>
    public class InMemoryReadModelStore<TKey, TModel> : IReadModelStore<TKey, TModel>
        where TKey : notnull
        where TModel : class
    {
        private readonly ConcurrentDictionary<TKey, TModel> _store = new();

        public void Insert(TKey id, TModel model)
        {
            _store.TryAdd(id, model);
        }

        public void Insert(TKey id, TModel model, DbConnection connection, DbTransaction transaction)
        {
            Insert(id, model);
        }

        public void Update(TKey id, Func<TModel, TModel> updater)
        {
            if (_store.TryGetValue(id, out var existing))
            {
                _store[id] = updater(existing);
            }
        }

        public void Update(TKey id, Func<TModel, TModel> updater, DbConnection connection, DbTransaction transaction)
        {
            Update(id, updater);
        }

        public void Upsert(TKey id, TModel model)
        {
            _store[id] = model;
        }

        public void Upsert(TKey id, TModel model, DbConnection connection, DbTransaction transaction)
        {
            Upsert(id, model);
        }

        public TModel GetById(TKey id)
        {
            return _store.TryGetValue(id, out var model) ? model : default;
        }

        public IReadOnlyList<TModel> GetAll()
        {
            return _store.Values.ToList();
        }

        public IReadOnlyList<TModel> GetWhere(Func<TModel, bool> predicate)
        {
            return _store.Values.Where(predicate).ToList();
        }

        public IReadOnlyList<TModel> GetWhere(string whereClause, object parameters)
        {
            // In-memory store doesn't support SQL - return all (default behavior)
            return GetAll();
        }

        /// <summary>
        /// Clears all data. Useful for test setup/teardown.
        /// </summary>
        public void Clear()
        {
            _store.Clear();
        }

        /// <summary>
        /// Gets the count of entities in the store.
        /// </summary>
        public int Count => _store.Count;
    }
}
