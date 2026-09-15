using ReactiveDomain.Messaging;

namespace ReactiveDomain.Foundation;

public interface IStreamReader : IDisposable {
	/// <summary>
	/// Where this read left the stream: the last event actually read, or the checkpoint it was asked
	/// to resume after when nothing newer existed. Null before the first <c>Read</c>, and after a
	/// <c>Read</c> from the beginning of a stream that is empty.
	/// </summary>
	long? Position { get; }
	/// <summary>
	/// Where this read left off: the stream, its version, and the <c>$all</c> position of the last
	/// event read. After a resume that found nothing newer, the version is the checkpoint it was
	/// asked to start from — not null, which would send a listener back to the beginning of the stream.
	/// Null only before the first <c>Read</c>, or after a <c>Read</c> of a stream that does not exist.
	/// </summary>
	/// <remarks>
	/// Read together deliberately — see <see cref="IListener.Checkpoint"/> for why the two clocks must
	/// not be sampled separately.
	/// </remarks>
	StreamCheckpoint? Checkpoint { get; }
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
	/// Replaces the handle with one that receives each event paired with the checkpoint this reader
	/// stands at once that event is read — the read-phase counterpart of
	/// <see cref="IListener.SubscribeToDelivery"/>.
	/// </summary>
	/// <remarks>
	/// The default cannot pair and routes through <see cref="Handle"/> with a null checkpoint;
	/// implementations that know each event's position should override it.
	/// </remarks>
	Action<IMessage, StreamCheckpoint?> PairedHandle {
		set => Handle = message => value(message, null);
	}

	/// <summary>
	/// Reads the events on a named stream
	/// </summary>
	/// <param name="stream">the exact stream name</param>
	/// <param name="completionCheck">Read will block until true to ensure processing has completed, use '()=> true' to continue without blocking. If cancellation or timeout is required it should be implemented in the completion method</param>
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
	/// <param name="completionCheck">Read will block until true to ensure processing has completed, use '()=> true' to continue without blocking. If cancellation or timeout is required it should be implemented in the completion method</param>
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
	/// <param name="completionCheck">Read will block until true to ensure processing has completed, use '()=> true' to continue without blocking. If cancellation or timeout is required it should be implemented in the completion method</param>
	/// <param name="checkpoint">start point to listen from</param>
	/// <param name="count">The count of items to read</param>
	/// <param name="readBackwards">read the stream backwards</param>
	/// <returns>Returns true if any events were read from the stream</returns>
	bool Read<TAggregate>(Guid id, Func<bool> completionCheck, long? checkpoint = null, long? count = null, bool readBackwards = false) where TAggregate : class, IEventSource;

	/// <summary>
	/// Reads the events on an Aggregate Category Stream
	/// </summary>
	/// <typeparam name="TAggregate">The type of aggregate</typeparam>
	/// <param name="completionCheck">Read will block until true to ensure processing has completed, use '()=> true' to continue without blocking. If cancellation or timeout is required it should be implemented in the completion method</param>
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
