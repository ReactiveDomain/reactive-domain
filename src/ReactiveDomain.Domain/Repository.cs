using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.ClientAPI;

namespace ReactiveDomain
{
    /// <summary>
    /// Generic repository that allows reading and writing of entities.
    /// </summary>
    public class Repository<TRoot> : IRepository<TRoot> where TRoot : AggregateRootEntity
    {
        private readonly EventSourceReader _reader;
        private readonly EventSourceWriter _writer;

        public Repository(
            Func<TRoot> factory, 
            IEventStoreConnection connection, 
            EventSourceReaderConfiguration readerConfiguration,
            EventSourceWriterConfiguration writerConfiguration)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            if (readerConfiguration == null)
            {
                throw new ArgumentNullException(nameof(readerConfiguration));
            }

            if (writerConfiguration == null)
            {
                throw new ArgumentNullException(nameof(writerConfiguration));
            }

            _reader = new EventSourceReader(
                factory, 
                connection, 
                readerConfiguration);
            _writer = new EventSourceWriter(
                connection,
                writerConfiguration
            );
        }

        /// <summary>
        /// Loads an entity by replaying events of the specified stream into it. 
        /// </summary>
        /// <param name="stream">The stream to load events from.</param>
        /// <param name="ct">The cancellation token (optional).</param>
        /// <returns>An entity on which the events have been replayed.</returns>
        /// <exception cref="StreamNotFoundException">Thrown when the stream was not found.</exception>
        /// <exception cref="StreamDeletedException">Thrown when the stream was deleted.</exception>
        public async Task<TRoot> LoadAsync(StreamName stream, CancellationToken ct = default(CancellationToken))
        {
            var result = await _reader.ReadStreamAsync(stream, ct).ConfigureAwait(false);
            switch(result.State)
            {
                case ReadResultState.Deleted:
                    throw new StreamDeletedException(stream);
                case ReadResultState.NotFound:
                    throw new StreamNotFoundException(stream);
            }
            return (TRoot)result.Value;
        }
        
        /// <summary>
        /// Attempts to load an entity by replaying events of the specified stream into it. 
        /// </summary>
        /// <param name="stream">The stream to load events from.</param>
        /// <param name="ct">The cancellation token (optional).</param>
        /// <returns>An entity on which the events have been replayed or <c>null</c> if the stream was not found..</returns>
        /// <exception cref="StreamDeletedException">Thrown when the stream was deleted.</exception>
        public async Task<TRoot> TryLoadAsync(StreamName stream, CancellationToken ct = default(CancellationToken))
        {
            var result = await _reader.ReadStreamAsync(stream, ct).ConfigureAwait(false);
            if(result.State == ReadResultState.Deleted)
                throw new StreamDeletedException(stream);
            return result.State == ReadResultState.NotFound
                ? default(TRoot)
                : (TRoot)result.Value;
        }
        
        /// <summary>
        /// Saves an entity by appending its events to the specified stream. 
        /// </summary>
        /// <param name="stream">The stream to append events to.</param>
        /// <param name="instance">The entity to take events from.</param>
        /// <param name="causation">The causation identifier.</param>
        /// <param name="correlation">The correlation identifier.</param>
        /// <param name="metadata">Any meta data to append with each event.</param>
        /// <param name="ct">The cancellation token (optional).</param>
        public Task SaveAsync(StreamName stream, TRoot instance, Guid causation, Guid correlation, Metadata metadata = null, CancellationToken ct = default(CancellationToken))
        {
            return _writer.WriteStreamAsync(stream, instance, causation, correlation, metadata, ct);
        }
    }
}