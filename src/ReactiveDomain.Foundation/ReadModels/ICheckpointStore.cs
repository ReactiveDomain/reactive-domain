using System.Data.Common;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation
{
    /// <summary>
    /// Interface for persisting projection checkpoints.
    /// Checkpoints track the last processed event position for each projection.
    /// Supports transactional operations via connection/transaction overloads.
    /// </summary>
    public interface ICheckpointStore
    {
        /// <summary>
        /// Gets the last checkpoint for a projection.
        /// </summary>
        /// <param name="projectionName">The name of the projection.</param>
        /// <returns>The checkpoint position, or null if no checkpoint exists.</returns>
        long? GetCheckpoint(string projectionName);

        /// <summary>
        /// Saves a checkpoint for a projection.
        /// </summary>
        /// <param name="projectionName">The name of the projection.</param>
        /// <param name="position">The checkpoint position.</param>
        void SaveCheckpoint(string projectionName, long position);

        /// <summary>
        /// Saves a checkpoint for a projection within a transaction.
        /// </summary>
        /// <param name="projectionName">The name of the projection.</param>
        /// <param name="position">The checkpoint position.</param>
        /// <param name="connection">The database connection.</param>
        /// <param name="transaction">The database transaction.</param>
        void SaveCheckpoint(string projectionName, long position, DbConnection connection, DbTransaction transaction);
    }
}
