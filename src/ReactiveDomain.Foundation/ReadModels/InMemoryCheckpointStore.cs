using System.Collections.Generic;
using System.Data.Common;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation
{
    /// <summary>
    /// In-memory implementation of ICheckpointStore for testing.
    /// </summary>
    public class InMemoryCheckpointStore : ICheckpointStore
    {
        private readonly Dictionary<string, long> _checkpoints = new();

        public long? GetCheckpoint(string projectionName)
        {
            return _checkpoints.TryGetValue(projectionName, out var position) ? position : null;
        }

        public void SaveCheckpoint(string projectionName, long position)
        {
            _checkpoints[projectionName] = position;
        }

        public void SaveCheckpoint(string projectionName, long position, DbConnection connection, DbTransaction transaction)
        {
            SaveCheckpoint(projectionName, position);
        }

        /// <summary>
        /// Gets all stored checkpoints. Useful for testing.
        /// </summary>
        public IReadOnlyDictionary<string, long> GetAllCheckpoints() => _checkpoints;

        /// <summary>
        /// Clears all checkpoints. Useful for test setup.
        /// </summary>
        public void Clear() => _checkpoints.Clear();
    }
}
