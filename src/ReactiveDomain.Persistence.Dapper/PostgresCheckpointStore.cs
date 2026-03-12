using System;
using System.Data.Common;
using Dapper;
using Npgsql;
using ReactiveDomain.Foundation;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Persistence.Dapper
{
    /// <summary>
    /// PostgreSQL implementation of ICheckpointStore using Dapper.
    /// Stores checkpoints in the projection_checkpoints table.
    /// </summary>
    public class PostgresCheckpointStore : ICheckpointStore
    {
        private readonly NpgsqlDataSource _dataSource;

        public PostgresCheckpointStore(NpgsqlDataSource dataSource)
        {
            _dataSource = dataSource;
        }

        public long? GetCheckpoint(string projectionName)
        {
            using var conn = _dataSource.OpenConnection();
            return conn.QueryFirstOrDefault<long?>(
                "SELECT last_position FROM projection_checkpoints WHERE projection_name = @Name",
                new { Name = projectionName });
        }

        public void SaveCheckpoint(string projectionName, long position)
        {
            using var conn = _dataSource.OpenConnection();
            SaveCheckpointInternal(projectionName, position, conn, null);
        }

        public void SaveCheckpoint(string projectionName, long position, DbConnection connection, DbTransaction transaction)
        {
            SaveCheckpointInternal(projectionName, position, connection, transaction);
        }

        private static void SaveCheckpointInternal(string projectionName, long position, DbConnection conn, DbTransaction tx)
        {
            conn.Execute(@"
                INSERT INTO projection_checkpoints (projection_name, last_position, updated_at)
                VALUES (@Name, @Position, @UpdatedAt)
                ON CONFLICT (projection_name) DO UPDATE SET last_position = @Position, updated_at = @UpdatedAt",
                new { Name = projectionName, Position = position, UpdatedAt = DateTime.UtcNow }, tx);
        }

        /// <summary>
        /// Creates the projection_checkpoints table if it doesn't exist.
        /// Call this during application startup.
        /// </summary>
        public void EnsureSchema()
        {
            using var conn = _dataSource.OpenConnection();
            conn.Execute(@"
                CREATE TABLE IF NOT EXISTS projection_checkpoints (
                    projection_name TEXT PRIMARY KEY,
                    last_position BIGINT NOT NULL,
                    updated_at TIMESTAMP WITH TIME ZONE NOT NULL DEFAULT NOW()
                )");
        }
    }
}
