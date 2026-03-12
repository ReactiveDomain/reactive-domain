using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation
{
    /// <summary>
    /// Interface for persisting read model snapshots.
    /// Abstracts the storage mechanism to enable testing with in-memory implementations.
    /// Supports transactional operations via optional connection/transaction parameters.
    /// </summary>
    /// <typeparam name="TKey">The type of the entity key (typically Guid).</typeparam>
    /// <typeparam name="TModel">The type of the read model entity.</typeparam>
    public interface IReadModelStore<TKey, TModel> where TKey : notnull
    {
        /// <summary>
        /// Inserts a new entity. Does nothing if the entity already exists.
        /// </summary>
        void Insert(TKey id, TModel model);

        /// <summary>
        /// Inserts a new entity within a transaction. Does nothing if the entity already exists.
        /// </summary>
        void Insert(TKey id, TModel model, DbConnection connection, DbTransaction transaction);

        /// <summary>
        /// Updates an existing entity by replacing it with a new version.
        /// Uses a functional update pattern for immutable records.
        /// Does nothing if the entity doesn't exist.
        /// </summary>
        void Update(TKey id, Func<TModel, TModel> updater);

        /// <summary>
        /// Updates an existing entity within a transaction.
        /// </summary>
        void Update(TKey id, Func<TModel, TModel> updater, DbConnection connection, DbTransaction transaction);

        /// <summary>
        /// Inserts or updates an entity.
        /// </summary>
        void Upsert(TKey id, TModel model);

        /// <summary>
        /// Inserts or updates an entity within a transaction.
        /// </summary>
        void Upsert(TKey id, TModel model, DbConnection connection, DbTransaction transaction);

        /// <summary>
        /// Gets an entity by ID, or default if not found.
        /// </summary>
        TModel GetById(TKey id);

        /// <summary>
        /// Gets all entities.
        /// </summary>
        IReadOnlyList<TModel> GetAll();

        /// <summary>
        /// Gets entities matching a predicate. 
        /// For SQL-backed stores, use the whereClause overload for DB-level filtering.
        /// </summary>
        IReadOnlyList<TModel> GetWhere(Func<TModel, bool> predicate) => GetAll().Where(predicate).ToList();

        /// <summary>
        /// Gets entities matching a SQL WHERE clause with parameters.
        /// Falls back to in-memory filtering for non-SQL stores.
        /// </summary>
        /// <param name="whereClause">SQL WHERE clause without the WHERE keyword (e.g., "program_id = @ProgramId")</param>
        /// <param name="parameters">Anonymous object with parameter values</param>
        IReadOnlyList<TModel> GetWhere(string whereClause, object parameters) => GetAll();
    }
}
