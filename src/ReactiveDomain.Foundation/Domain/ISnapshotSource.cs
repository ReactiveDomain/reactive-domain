namespace ReactiveDomain
{
    /// <summary>
    /// Represents a source of snapshots from the perspective of restoring from or taking snapshots. To be used by infrastructure code only.
    /// </summary>
    public interface ISnapshotSource
    {
        /// <summary>
        /// Restores this instance from a snapshot using the specified <paramref name="snapshot"/> object.
        /// </summary>
        /// <param name="snapshot">The object to restore the snapshot from.</param>
        void RestoreFromSnapshot(object snapshot);

        /// <summary>
        /// Takes a snapshot of this instance.
        /// </summary>
        /// <returns>The object that represents the snapshot.</returns>
        object TakeSnapshot();
    }
}