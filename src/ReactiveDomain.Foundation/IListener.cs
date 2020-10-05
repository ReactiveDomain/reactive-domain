using System;
using System.Threading;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Foundation
{
    public interface IListener : IDisposable
    {
        ISubscriber EventStream { get; }
        long Position { get; }
        string StreamName { get; }

        /// <summary>
        /// Starts listening on a named stream
        /// </summary>
        /// <param name="stream">the exact stream name</param>
        /// <param name="checkpoint">start point to listen from</param>
        /// <param name="blockUntilLive">wait for the is live event from the catchup subscription before returning</param>
        /// <param name="cancelWaitToken">Cancellation token to cancel waiting if blockUntilLive is true</param>
        void Start(string stream, long? checkpoint = null, bool blockUntilLive = false, CancellationToken cancelWaitToken = default(CancellationToken));

        /// <summary>
        /// Starts listening on an aggregate root stream
        /// </summary>
        /// <typeparam name="TAggregate">The type of aggregate</typeparam>
        /// <param name="id">the aggregate id</param>
        /// <param name="checkpoint">start point to listen from</param>
        /// <param name="blockUntilLive">wait for the is live event from the catchup subscription before returning</param>
        /// <param name="cancelWaitToken">Cancellation token to cancel waiting if blockUntilLive is true</param>
        void Start<TAggregate>(Guid id, long? checkpoint = null, bool blockUntilLive = false, CancellationToken cancelWaitToken = default(CancellationToken)) where TAggregate : class, IEventSource;

        /// <summary>
        /// Starts listening on a Aggregate Category Stream
        /// </summary>
        /// <typeparam name="TAggregate">The type of aggregate</typeparam>
        /// <param name="checkpoint">start point to listen from</param>
        /// <param name="blockUntilLive">wait for the is live event from the catchup subscription before returning</param>
        /// <param name="cancelWaitToken">Cancellation token to cancel waiting if blockUntilLive is true</param>
        void Start<TAggregate>(long? checkpoint = null, bool blockUntilLive = false, CancellationToken cancelWaitToken = default(CancellationToken)) where TAggregate : class, IEventSource;
    }
}
