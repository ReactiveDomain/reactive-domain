using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using System;


namespace ReactiveDomain.Foundation
{
    public interface IStreamReader : IDisposable
    {
        
      
        
        /// <summary>
        /// The ending position of the stream after the read is complete
        /// </summary>
        long? Position { get; }
        /// <summary>
        /// The name of the stream being read
        /// </summary>
        string StreamName { get; }

        /// <summary>
        /// The updatable handle for the events.
        /// If set replaces the existing target/handle.
        /// </summary>
        Action<IMessage> Handle { set; }

        /// <summary>
        /// Reads the events on a named stream
        /// </summary>
        /// <param name="stream">the exact stream name</param>
        /// <param name="completionCheck">Read will block until true to ensure processing has completed, use '()=> true' to continue without blocking. If cancelation or timeout is required it should be implemented in the completion method</param>
        /// <param name="checkpoint">start point to listen from</param>
        /// <param name="count">The count of items to read</param>
        /// <param name="readBackwards">read the stream backwards</param>
        /// <returns>Returns true if any events were read from the stream</returns>
        bool Read(string stream, Func<bool> completionCheck, long? checkpoint = null, long? count = null, bool readBackwards = false);

        /// <summary>
        /// By Event Type Projection Reader
        /// i.e. $et-[MessageType]
        /// </summary>
        /// <param name="tMessage">The message type used to generate the stream (projection) name</param>
        /// <param name="completionCheck">Read will block until true to ensure processing has completed, use '()=> true' to continue without blocking. If cancelation or timeout is required it should be implemented in the completion method</param>
        /// <param name="checkpoint">The starting point to read from.</param>
        /// <param name="count">The count of items to read</param>
        /// <param name="readBackwards">Read the stream backwards</param>
        /// <returns>Returns true if any events were read from the stream</returns>
        bool Read(Type tMessage, Func<bool> completionCheck, long? checkpoint = null, long? count = null, bool readBackwards = false);

        /// <summary>
        /// Reads the events on an aggregate root stream
        /// </summary>
        /// <typeparam name="TAggregate">The type of aggregate</typeparam>
        /// <param name="id">the aggregate id</param>
        /// <param name="completionCheck">Read will block until true to ensure processing has completed, use '()=> true' to continue without blocking. If cancelation or timeout is required it should be implemented in the completion method</param>
        /// <param name="checkpoint">start point to listen from</param>
        /// <param name="count">The count of items to read</param>
        /// <param name="readBackwards">read the stream backwards</param>
        /// <returns>Returns true if any events were read from the stream</returns>
        bool Read<TAggregate>(Guid id, Func<bool> completionCheck, long? checkpoint = null, long? count = null, bool readBackwards = false) where TAggregate : class, IEventSource;

        /// <summary>
        /// Reads the events on a Aggregate Category Stream
        /// </summary>
        /// <typeparam name="TAggregate">The type of aggregate</typeparam>
        /// <param name="completionCheck">Read will block until true to ensure processing has completed, use '()=> true' to continue without blocking. If cancelation or timeout is required it should be implemented in the completion method</param>
        /// <param name="checkpoint">start point to listen from</param>
        /// <param name="count">The count of items to read</param>
        /// <param name="readBackwards">read the stream backwards</param>
        /// <returns>Returns true if any events were read from the stream</returns>
        bool Read<TAggregate>(Func<bool> completionCheck, long? checkpoint = null, long? count = null, bool readBackwards = false) where TAggregate : class, IEventSource;


        /// <summary>
        /// Interrupts the reading process. Doesn't guarantee the moment when reading is stopped. For optimization purpose.
        /// </summary>
        void Cancel();
    }
}
