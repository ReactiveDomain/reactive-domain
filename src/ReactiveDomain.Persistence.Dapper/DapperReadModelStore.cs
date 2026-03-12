using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Dapper;
using Npgsql;
using ReactiveDomain.Foundation;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Persistence.Dapper
{
    /// <summary>
    /// Generic Dapper-based read model store for PostgreSQL.
    /// Uses POCO classes with [Table] and [Column] attributes for mapping.
    /// Supports transactional operations via connection/transaction overloads.
    /// </summary>
    /// <typeparam name="TKey">The type of the entity key.</typeparam>
    /// <typeparam name="TModel">The type of the read model entity (must be a class with parameterless constructor).</typeparam>
    public class DapperReadModelStore<TKey, TModel> : IReadModelStore<TKey, TModel>
        where TKey : notnull
        where TModel : class, new()
    {
        private readonly NpgsqlDataSource _dataSource;
        private readonly string _tableName;
        private readonly string _keyColumn;

        public DapperReadModelStore(NpgsqlDataSource dataSource, string tableName, string keyColumn)
        {
            _dataSource = dataSource;
            _tableName = tableName;
            _keyColumn = keyColumn;
        }

        /// <summary>
        /// Opens a new connection for transactional operations.
        /// Caller is responsible for disposing the connection.
        /// </summary>
        public NpgsqlConnection OpenConnection() => _dataSource.OpenConnection();

        public void Insert(TKey id, TModel model)
        {
            using var conn = _dataSource.OpenConnection();
            InsertInternal(model, conn, null);
        }

        public void Insert(TKey id, TModel model, DbConnection connection, DbTransaction transaction)
        {
            InsertInternal(model, connection, transaction);
        }

        private void InsertInternal(TModel model, DbConnection conn, DbTransaction tx)
        {
            var columns = GetColumnMappings();
            var columnNames = string.Join(", ", columns.Select(c => c.ColumnName));
            var paramNames = string.Join(", ", columns.Select(c => $"@{c.PropertyName}"));

            var sql = $@"
                INSERT INTO {_tableName} ({columnNames})
                VALUES ({paramNames})
                ON CONFLICT ({_keyColumn}) DO NOTHING";

            conn.Execute(sql, model, tx);
        }

        public void Update(TKey id, Func<TModel, TModel> updater)
        {
            var existing = GetById(id);
            if (existing != null)
            {
                var updated = updater(existing);
                Upsert(id, updated);
            }
        }

        public void Update(TKey id, Func<TModel, TModel> updater, DbConnection connection, DbTransaction transaction)
        {
            var existing = GetByIdInternal(id, connection, transaction);
            if (existing != null)
            {
                var updated = updater(existing);
                UpsertInternal(updated, connection, transaction);
            }
        }

        public void Upsert(TKey id, TModel model)
        {
            using var conn = _dataSource.OpenConnection();
            UpsertInternal(model, conn, null);
        }

        public void Upsert(TKey id, TModel model, DbConnection connection, DbTransaction transaction)
        {
            UpsertInternal(model, connection, transaction);
        }

        private void UpsertInternal(TModel model, DbConnection conn, DbTransaction tx)
        {
            var columns = GetColumnMappings();
            var columnNames = string.Join(", ", columns.Select(c => c.ColumnName));
            var paramNames = string.Join(", ", columns.Select(c => $"@{c.PropertyName}"));
            var updateColumns = columns.Where(c => c.ColumnName != _keyColumn);
            var updateSet = string.Join(", ", updateColumns.Select(c => $"{c.ColumnName} = @{c.PropertyName}"));

            var sql = $@"
                INSERT INTO {_tableName} ({columnNames})
                VALUES ({paramNames})
                ON CONFLICT ({_keyColumn}) DO UPDATE SET {updateSet}";

            conn.Execute(sql, model, tx);
        }

        public TModel GetById(TKey id)
        {
            using var conn = _dataSource.OpenConnection();
            return GetByIdInternal(id, conn, null);
        }

        private TModel GetByIdInternal(TKey id, DbConnection conn, DbTransaction tx)
        {
            var sql = $"SELECT {GetSelectColumns()} FROM {_tableName} WHERE {_keyColumn} = @Id";
            return conn.QueryFirstOrDefault<TModel>(sql, new { Id = id }, tx);
        }

        public IReadOnlyList<TModel> GetAll()
        {
            using var conn = _dataSource.OpenConnection();
            var sql = $"SELECT {GetSelectColumns()} FROM {_tableName}";
            return conn.Query<TModel>(sql).ToList();
        }

        public IReadOnlyList<TModel> GetWhere(Func<TModel, bool> predicate)
        {
            return GetAll().Where(predicate).ToList();
        }

        public IReadOnlyList<TModel> GetWhere(string whereClause, object parameters)
        {
            using var conn = _dataSource.OpenConnection();
            var sql = $"SELECT {GetSelectColumns()} FROM {_tableName} WHERE {whereClause}";
            return conn.Query<TModel>(sql, parameters).ToList();
        }

        /// <summary>
        /// Builds a SELECT column list with aliases to map snake_case columns to PascalCase properties.
        /// Example: "program_name AS ProgramName, model_year AS ModelYear"
        /// </summary>
        private static string GetSelectColumns()
        {
            var columns = GetColumnMappings();
            return string.Join(", ", columns.Select(c => $"{c.ColumnName} AS {c.PropertyName}"));
        }

        private sealed record ColumnMapping(string PropertyName, string ColumnName);

        private static List<ColumnMapping> GetColumnMappings()
        {
            var result = new List<ColumnMapping>();
            var properties = typeof(TModel).GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                var columnAttr = prop.GetCustomAttribute<ColumnAttribute>();
                var columnName = columnAttr?.Name ?? ToSnakeCase(prop.Name);
                result.Add(new ColumnMapping(prop.Name, columnName));
            }

            return result;
        }

        private static string ToSnakeCase(string name)
        {
            return string.Concat(name.Select((c, i) =>
                i > 0 && char.IsUpper(c)
                    ? "_" + char.ToLower(c, CultureInfo.InvariantCulture)
                    : char.ToLower(c, CultureInfo.InvariantCulture).ToString()));
        }
    }
}
