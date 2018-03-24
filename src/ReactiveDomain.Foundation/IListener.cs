using System;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Foundation
{
    public interface IListener : IDisposable
    {
        ISubscriber EventStream { get; }

        /// <summary>
        /// Starts listening on a named stream
        /// </summary>
        /// <param name="stream">the exact stream name</param>
        /// <param name="checkpoint">start point to listen from</param>
        /// <param name="blockUntilLive">wait for the is live event from the catchup subscription before returning</param>
        /// <param name="millisecondsTimeout">Timeout to wait before aborting Load defaults to 1000ms</param>
        void Start(string stream, int? checkpoint = null, bool blockUntilLive = false, int millisecondsTimeout = 1000);
        /// <summary>
        /// Starts listening on an aggregate root stream
        /// </summary>
        /// <typeparam name="TAggregate">The type of aggregate</typeparam>
        /// <param name="id">the aggregate id</param>
        /// <param name="checkpoint">start point to listen from</param>
        /// <param name="blockUntilLive">wait for the is live event from the catchup subscription before returning</param>
        /// <param name="millisecondsTimeout">Timeout to wait before aborting Load defaults to 1000ms</param>
        void Start<TAggregate>(Guid id, int? checkpoint = null, bool blockUntilLive = false, int millisecondsTimeout = 1000) where TAggregate : class, IEventSource;
        /// <summary>
        /// Starts listening on a Aggregate Category Stream
        /// </summary>
        /// <typeparam name="TAggregate">The type of aggregate</typeparam>
        /// <param name="checkpoint">start point to listen from</param>
        /// <param name="blockUntilLive">wait for the is live event from the catchup subscription before returning</param>
        /// <param name="millisecondsTimeout">Timeout to wait before aborting Load defaults to 1000ms</param>
        void Start<TAggregate>(int? checkpoint = null, bool blockUntilLive = false, int millisecondsTimeout = 1000) where TAggregate : class, IEventSource;
    }
}
