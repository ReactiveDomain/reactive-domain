using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Foundation;

public interface IListener : IDisposable {
	ISubscriber EventStream { get; }

	/// <summary>The version of the last event delivered from <see cref="StreamName"/>.</summary>
	long Position { get; }

	/// <summary>
	/// How far this listener has delivered: its stream, that stream's version, and the <c>$all</c>
	/// position of the last event, read as one value. Null before the listener has started, when it
	/// has no stream to report.
	/// </summary>
	/// <remarks>
	/// Read together deliberately. Taken separately, a version and a position can come from different
	/// events, producing a checkpoint whose position claims an event its version says was never
	/// applied.
	/// </remarks>
	StreamCheckpoint? Checkpoint { get; }

	/// <summary>
	/// Adopts the <c>$all</c> position of history applied before this listener started, so a
	/// checkpoint taken before the first live event still covers what the reader handled. Any event
	/// this listener delivers replaces it.
	/// </summary>
	void SeedAllPosition(Position? position);

	string StreamName { get; }

	/// <summary>
	/// Starts listening on a named stream
	/// </summary>
	/// <param name="stream">the exact stream name</param>
	/// <param name="checkpoint">start point to listen from</param>
	/// <param name="blockUntilLive">wait for the is live event from the catchup subscription before returning</param>
	/// <param name="validateStream">ensure the stream exists on start</param>
	/// <param name="cancelWaitToken">Cancellation token to cancel waiting if blockUntilLive is true</param>
	void Start(string stream, long? checkpoint = null, bool blockUntilLive = false, bool validateStream = false, CancellationToken cancelWaitToken = default);
	/// <summary>
	/// Starts listening on an aggregate root stream
	/// </summary>
	/// <typeparam name="TAggregate">The type of aggregate</typeparam>
	/// <param name="id">the aggregate id</param>
	/// <param name="checkpoint">start point to listen from</param>
	/// <param name="blockUntilLive">wait for the is live event from the catchup subscription before returning</param>
	/// <param name="validateStream">ensure the stream exists on start</param>
	/// <param name="cancelWaitToken">Cancellation token to cancel waiting if blockUntilLive is true</param>
	void Start<TAggregate>(Guid id, long? checkpoint = null, bool blockUntilLive = false, bool validateStream = false, CancellationToken cancelWaitToken = default) where TAggregate : class, IEventSource;
	/// <summary>
	/// Starts listening on a Aggregate Category Stream
	/// </summary>
	/// <typeparam name="TAggregate">The type of aggregate</typeparam>
	/// <param name="checkpoint">start point to listen from</param>
	/// <param name="blockUntilLive">wait for the is live event from the catchup subscription before returning</param>
	/// <param name="validateStream">ensure the stream exists on start</param>
	/// <param name="cancelWaitToken">Cancellation token to cancel waiting if blockUntilLive is true</param>
	void Start<TAggregate>(long? checkpoint = null, bool blockUntilLive = false, bool validateStream = false, CancellationToken cancelWaitToken = default) where TAggregate : class, IEventSource;
}
