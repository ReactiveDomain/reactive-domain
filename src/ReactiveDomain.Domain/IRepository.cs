using System;
using System.Threading;
using System.Threading.Tasks;

namespace ReactiveDomain
{
    /// <summary>
    /// Generic repository that allows reading and writing of entities.
    /// </summary>
    public interface IRepository<TRoot> where TRoot : AggregateRootEntity
    {
        /// <summary>
        /// Loads an entity by replaying events of the specified stream into it. 
        /// </summary>
        /// <param name="stream">The stream to load events from.</param>
        /// <param name="ct">The cancellation token (optional).</param>
        /// <returns>An entity on which the events have been replayed.</returns>
        /// <exception cref="StreamNotFoundException">Thrown when the stream was not found.</exception>
        /// <exception cref="StreamDeletedException">Thrown when the stream was deleted.</exception>
        Task<TRoot> LoadAsync(StreamName stream, CancellationToken ct = default(CancellationToken));
        /// <summary>
        /// Attempts to load an entity by replaying events of the specified stream into it. 
        /// </summary>
        /// <param name="stream">The stream to load events from.</param>
        /// <param name="ct">The cancellation token (optional).</param>
        /// <returns>An entity on which the events have been replayed or <c>null</c> if the stream was not found..</returns>
        /// <exception cref="StreamDeletedException">Thrown when the stream was deleted.</exception>
        Task<TRoot> TryLoadAsync(StreamName stream, CancellationToken ct = default(CancellationToken));
        /// <summary>
        /// Saves an entity by appending its events to the specified stream. 
        /// </summary>
        /// <param name="stream">The stream to append events to.</param>
        /// <param name="instance">The entity to take events from.</param>
        /// <param name="causation">The causation identifier.</param>
        /// <param name="correlation">The correlation identifier.</param>
        /// <param name="metadata">Any meta data to append with each event.</param>
        /// <param name="ct">The cancellation token (optional).</param>
        Task SaveAsync(StreamName stream, TRoot instance, Guid causation, Guid correlation, Metadata metadata = null, CancellationToken ct = default(CancellationToken));
    }
}