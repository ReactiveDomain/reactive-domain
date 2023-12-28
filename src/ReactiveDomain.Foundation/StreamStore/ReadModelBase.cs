using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Util;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation
{
    public abstract class ReadModelBase :
        IHandle<IMessage>,
        IHandle<Message>,
        IPublisher,
        IDisposable
    {
        private readonly Func<IListener> _getListener;
        private readonly List<IListener> _listeners;
        private readonly Func<IStreamReader> _getReader;
        private readonly InMemoryBus _bus;
        private readonly QueuedHandler _queue;
        public int MessageCount => _queue.MessageCount;
        public bool Idle => _queue.Idle;

        /// <summary>
        /// ReaderLock locks the event handler and can be used when reading the model 
        /// to ensure model state is unchanged during read.
        /// The lock should *not* be used in Handle methods as they are inside the lock already by default.
        /// </summary>
        protected readonly object ReaderLock = new object();

        /// <summary>
        /// The version is equal to the number of messages passed to the read model.
        /// The version is incremented after all handlers have been processed.
        /// The number of handlers (including none) will not impact the version.
        /// This can be used to ensure read model state for tests. This is *not*
        /// the same as the version of any particular stream being read.
        /// </summary>
        public int Version { get; private set; }

        /// <summary>
        /// Creates a read model using the provided stream store connection. Reads existing events using a
        /// reader, then transitions to a listener for live events.
        /// </summary>
        /// <param name="name">The name of the read model. Also used as the names of the listener and reader.</param>
        /// <param name="connection">A connection to a stream store.</param>
        protected ReadModelBase(string name, IConfiguredConnection connection)
        {
            Ensure.NotNull(connection, nameof(connection));
            _getReader = () => connection.GetReader(name, Handle);
            _getListener = () => connection.GetListener(name);
            _listeners = new List<IListener>();
            _bus = new InMemoryBus($"{nameof(ReadModelBase)}:{name} bus", false);
            _queue = new QueuedHandler(new AdHocHandler<IMessage>(DequeueMessage), $"{nameof(ReadModelBase)}:{name} queue");
            _queue.Start();
        }

        /// <summary>
        /// Every message handled by the read model will pass through here.
        /// </summary>
        private void DequeueMessage(IMessage message)
        {
            lock (ReaderLock)
            {
                _bus.Handle(message);
                Version++;
            }
        }

        private IListener AddNewListener()
        {
            var l = _getListener();
            lock (_listeners)
            {
                _listeners.Add(l);
            }
            l.EventStream.SubscribeToAll(_queue);
            return l;
        }

        /// <summary>
        /// Get the positions of all listeners.
        /// </summary>
        /// <returns>A list of Tuples of listener names and checkpoints.</returns>
        public List<Tuple<string, long>> GetCheckpoint()
        {
            lock (_listeners)
            {
                return _listeners.Select(l => new Tuple<string, long>(l.StreamName, l.Position)).ToList();
            }
        }

        /// <summary>
        /// The stream of events that handlers should subscribe to.
        /// </summary>
        public ISubscriber EventStream => _bus;

        /// <summary>
        /// Start playback of a named stream.
        /// </summary>
        /// <param name="stream">The name of the stream to play back.</param>
        /// <param name="checkpoint">The event to start with.</param>
        /// <param name="blockUntilLive">If true, blocks returning from this method until the listener has caught up.</param>
        /// <param name="cancelWaitToken">Cancellation token to cancel waiting if blockUntilLive is true.</param>
        public void Start(string stream, long? checkpoint = null, bool blockUntilLive = false, CancellationToken cancelWaitToken = default)
        {
            if (_getReader != null)
            {
                using (var reader = _getReader())
                {
                    reader.Read(stream, () => Idle, checkpoint);
                    checkpoint = reader.Position ?? checkpoint;
                }
            }
            AddNewListener().Start(stream, checkpoint, blockUntilLive, cancelWaitToken);
        }

        /// <summary>
        /// Start playback of a specific stream of type TAggregate.
        /// </summary>
        /// <typeparam name="TAggregate">The type of stream to play back.</typeparam>
        /// <param name="id">The ID of the stream to play back.</param>
        /// <param name="checkpoint">The event to start with.</param>
        /// <param name="blockUntilLive">If true, blocks returning from this method until the listener has caught up.</param>
        /// <param name="cancelWaitToken">Cancellation token to cancel waiting if blockUntilLive is true.</param>
        public void Start<TAggregate>(Guid id, long? checkpoint = null, bool blockUntilLive = false, CancellationToken cancelWaitToken = default) where TAggregate : class, IEventSource
        {
            if (_getReader != null)
            {
                using (var reader = _getReader())
                {
                    reader.Read<TAggregate>(id, () => Idle, checkpoint);
                    checkpoint = reader.Position;
                }
            }
            AddNewListener().Start<TAggregate>(id, checkpoint, blockUntilLive, cancelWaitToken);
        }

        /// <summary>
        /// Start a category listener for type TAggregate.
        /// </summary>
        /// <typeparam name="TAggregate">The type of stream to play back.</typeparam>
        /// <param name="checkpoint">The event to start with.</param>
        /// <param name="blockUntilLive">If true, blocks returning from this method until the listener has caught up.</param>
        /// <param name="cancelWaitToken">Cancellation token to cancel waiting if blockUntilLive is true.</param>
        public void Start<TAggregate>(long? checkpoint = null, bool blockUntilLive = false, CancellationToken cancelWaitToken = default) where TAggregate : class, IEventSource
        {
            if (_getReader != null)
            {
                using (var reader = _getReader())
                {
                    reader.Read<TAggregate>(() => Idle, checkpoint);
                    checkpoint = reader.Position;
                }
            }
            AddNewListener().Start<TAggregate>(checkpoint, blockUntilLive, cancelWaitToken);
        }

        /// <summary>
        /// Dispose of resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        private bool _disposed;
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                lock (_listeners)
                {
                    _listeners?.ForEach(l => l?.Dispose());
                }
                _queue?.RequestStop();
                _bus?.Dispose();
            }
            _disposed = true;
        }

        public void Handle(Message message) { ((IHandle<IMessage>)_queue).Handle(message); }
        public void Handle(IMessage message) { ((IHandle<IMessage>)_queue).Handle(message); }
        public void Publish(IMessage message) { ((IPublisher)_queue).Publish(message); }
    }
}
