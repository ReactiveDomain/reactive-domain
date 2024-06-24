using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using System;
using System.Threading;

namespace ReactiveDomain.Testing
{
    /// <summary>
    /// An empty listener that implements <see cref="IListener"/>.
    /// </summary>
    public class NullListener : IListener, ISubscriber
    {

        private string _stream;
        private long _position;
        /// <summary>
        /// Gets a <see cref="NullListener"/>
        /// </summary>
        public ISubscriber EventStream => this;

        /// <summary>
        /// Gets the position of the stream.
        /// </summary>
        public long Position => _position;

        /// <summary>
        /// Gets the name of the stream.
        /// </summary>
        public string StreamName => _stream;

        /// <summary>
        /// Creates an empty lister.
        /// </summary>
        /// <param name="name">This parameter is ignored.</param>
        public NullListener(string name = "")
        {          
        }

        /// <summary>
        /// Cleans up resources.
        /// </summary>
        public void Dispose()
        {
        }

        /// <summary>
        /// Starts the listener at the requested checkpoint. Since the listener is not connected to anything,
        /// this simply sets the listener's initial checkpoint.
        /// </summary>
        /// <param name="stream">The name of the stream.</param>
        /// <param name="checkpoint">The position at which the listener should start.</param>
        /// <param name="blockUntilLive">This parameter is ignored.</param>
        /// <param name="cancelWaitToken">This parameter is ignored.</param>
        public void Start(string stream, long? checkpoint = null, bool blockUntilLive = false, CancellationToken cancelWaitToken = default)
        {
            _stream = stream;
            _position = checkpoint ?? 0;
        }

        /// <summary>
        /// Does nothing. Required for implementation of <see cref="ISubscriber"/>.
        /// </summary>
        /// <param name="handler"></param>
        /// <returns></returns>
        public IDisposable SubscribeToAll(IHandle<IMessage> handler)
        {
            return this;
        }

        /// <summary>
        /// Does nothing. Required for implementation of <see cref="ISubscriber"/>.
        /// </summary>
        /// <typeparam name="T">This type parameter is ignored.</typeparam>
        /// <param name="includeDerived">This parameter is ignored.</param>
        /// <returns><c>false</c> since the listener does not actually subscribe to anything.</returns>
        bool ISubscriber.HasSubscriberFor<T>(bool includeDerived)
        {
            return false;
        }

        /// <summary>
        /// Does nothing other than set the stream name. Required for implementation of <see cref="IListener"/>.
        /// </summary>
        /// <typeparam name="TAggregate">The type of aggregate. Used for building the listener's stream name.</typeparam>
        /// <param name="id">The ID of the aggregate whose stream to listen to.</param>
        /// <param name="checkpoint">This parameter is ignored.</param>
        /// <param name="blockUntilLive">This parameter is ignored.</param>
        /// <param name="cancelWaitToken">This parameter is ignored.</param>
        void IListener.Start<TAggregate>(Guid id, long? checkpoint, bool blockUntilLive, CancellationToken cancelWaitToken)
        {
            _stream = new PrefixedCamelCaseStreamNameBuilder().GenerateForAggregate(typeof(TAggregate), id);
        }

        /// <summary>
        /// Does nothing other than set the stream name. Required for implementation of <see cref="IListener"/>.
        /// </summary>
        /// <typeparam name="TAggregate">The type of aggregate. Used for building the listener's stream name.</typeparam>
        /// <param name="checkpoint">This parameter is ignored.</param>
        /// <param name="blockUntilLive">This parameter is ignored.</param>
        /// <param name="cancelWaitToken">This parameter is ignored.</param>
        void IListener.Start<TAggregate>(long? checkpoint, bool blockUntilLive, CancellationToken cancelWaitToken)
        {
            _stream = nameof(TAggregate);
        }

        /// <summary>
        /// Does nothing. Required for implementation of <see cref="ISubscriber"/>.
        /// </summary>
        /// <typeparam name="T">This type parameter is ignored.</typeparam>
        /// <param name="handler">This parameter is ignored.</param>
        /// <param name="includeDerived">This parameter is ignored.</param>
        /// <returns></returns>
        IDisposable ISubscriber.Subscribe<T>(IHandle<T> handler, bool includeDerived)
        {
            return this;
        }

        /// <summary>
        /// Does nothing. Required for implementation of <see cref="ISubscriber"/>.
        /// </summary>
        /// <typeparam name="T">This type parameter is ignored.</typeparam>
        /// <param name="handler">This parameter is ignored.</param>
        void ISubscriber.Unsubscribe<T>(IHandle<T> handler)
        {
        }
    }
}
