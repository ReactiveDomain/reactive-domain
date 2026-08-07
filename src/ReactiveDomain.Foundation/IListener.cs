using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Foundation;

public interface IListener : IDisposable {
	/// <summary>The events this listener delivers, and its live transition.</summary>
	/// <remarks>
	/// A subscriber's handler runs inside the delivery, holding whatever
	/// <see cref="HoldDelivery"/> takes — that is what lets a checkpoint be sampled without a delivery
	/// half-done. So a handler here must not block on anything that waits on this listener, a capture
	/// of a model reading it, or another thread that might. Subscribe a queue rather than work.
	/// </remarks>
	ISubscriber EventStream { get; }

	/// <summary>
	/// The version of the last event delivered from <see cref="StreamName"/>, or 0 when none has
	/// been. Use <see cref="Checkpoint"/> to tell those two apart.
	/// </summary>
	long Position { get; }

	/// <summary>
	/// How far this listener has delivered: its stream, that stream's version, and the <c>$all</c>
	/// position of the last event, read as one value. Null before the listener has started, when it
	/// has no stream to report.
	/// </summary>
	/// <remarks>
	/// <para>Read together deliberately. Taken separately, a version and a position can come from
	/// different events, producing a checkpoint whose position claims an event its version says was
	/// never applied.</para>
	/// <para>Delivered means handed on, not handled: an implementation records a checkpoint only once
	/// it has published the event, but what it publishes to is a queue, and everything downstream of
	/// that queue is behind this.</para>
	/// </remarks>
	StreamCheckpoint? Checkpoint { get; }

	/// <summary>
	/// Adopts the <c>$all</c> position of history applied before this listener started, so a
	/// checkpoint taken before the first live event still covers what the reader handled. Any event
	/// this listener delivers replaces it.
	/// </summary>
	void SeedAllPosition(Position? position);

	/// <summary>Whether this listener has been disposed; a disposed listener delivers nothing further.</summary>
	bool IsDisposed { get; }

	/// <summary>
	/// Holds this listener's delivery, so that nothing is published to <see cref="EventStream"/> and
	/// <see cref="Checkpoint"/> cannot move, until the returned handle is disposed.
	/// </summary>
	/// <remarks>
	/// <para>What it excludes is a delivery <i>in progress</i>, not merely the next one. An
	/// implementation publishes an event and then records it, so a delivery caught between the two has
	/// already handed a subscriber an event that its checkpoint does not yet name; a reader that saw
	/// that would take a checkpoint behind its own subscriber. Holding delivery means no such straddle
	/// is in flight, so what has been published and what is checkpointed agree.</para>
	/// <para>Meant to be held across a sample and whatever the sampler does with it, and no longer:
	/// the listener's delivery thread is stopped for the duration. A caller holding several must take
	/// them in a consistent order.</para>
	/// <para><b>Release it on the thread that took it</b>, and so do not await while holding one — a
	/// continuation can resume elsewhere. Releasing from another thread throws and leaves the hold
	/// held, rather than reporting a release that did not happen.</para>
	/// <para>Releasing twice from the owning thread is a no-op.</para>
	/// </remarks>
	IDisposable HoldDelivery();

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
