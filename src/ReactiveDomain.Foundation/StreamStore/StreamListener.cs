using System.Reactive;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Util;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation;

/// <summary>
/// StreamListener
/// This class wraps a StreamStoreSubscription and is primarily used in the building of read models. 
/// The Raw events returned from the Stream will be unwrapped using the provided serializer and
/// consumers can subscribe to event notifications via the exposed EventStream.
///</summary>
/// <remarks>
/// N.B. The callbacks on the EventStream subscriptions will use the thread pool threads from the
/// Subscription and are not guaranteed to complete in order, especially if handlers require variable
/// amounts of time to complete processing. This can cause out of order events to be seen in the read model.
/// If event ordering is required use the QueuedListener or a QueuedHandler to dequeue the events in order.
/// </remarks> 
public class StreamListener : IListener {
	protected readonly string ListenerName;
	protected readonly InMemoryBus Bus;
	private IDisposable? _subscription;
	private bool _started;
	private readonly IStreamNameBuilder _streamNameBuilder;
	protected readonly IEventSerializer Serializer;
	private readonly Action<Unit>? _liveProcessingStarted;
	private readonly Action<SubscriptionDropReason, Exception?>? _subscriptionDropped;
	private readonly object _startLock = new();
	private readonly ManualResetEventSlim _liveLock = new();
	public bool IsLive => _liveLock.IsSet;
	public ISubscriber EventStream => Bus;
	private readonly IStreamStoreConnection _streamStoreConnection;
	protected long StreamPosition;
	public long Position => StreamPosition;

	private readonly object _checkpointLock = new();
	private Position? _allPosition;

	// False while StreamPosition still holds its seed rather than a version anything reached: a
	// listener started on a stream with no events on it. Zero cannot say that — see
	// StreamCheckpoint.Version.
	private bool _versioned;

	/// <inheritdoc cref="IListener.Checkpoint"/>
	public StreamCheckpoint? Checkpoint {
		get {
			lock (_checkpointLock) {
				// Named only once Start has run. Before that there is no stream to report, and the
				// version and position hold seed values that belong to nothing yet.
				return string.IsNullOrEmpty(StreamName)
					? null
					: new StreamCheckpoint(
						StreamName,
						_versioned ? Interlocked.Read(ref StreamPosition) : null,
						_allPosition);
			}
		}
	}

	/// <summary>
	/// Records how far this listener has delivered. Both clocks come from one event and are written
	/// under one lock, so a reader cannot see a version from one event beside a position from another.
	/// </summary>
	/// <remarks>
	/// <para>Call this <b>after</b> handing the event on, never before: until the publish returns, the
	/// event is in no queue, and a checkpoint naming it would claim an event nothing downstream can
	/// still reach.</para>
	/// <para>The position is assigned unconditionally, so a store that stops reporting positions leaves
	/// this null rather than stale — it belongs to the last event delivered, not to the last one that
	/// happened to carry one.</para>
	/// </remarks>
	/// <param name="recordedEvent">The event just delivered.</param>
	protected void RecordDelivered(RecordedEvent recordedEvent) {
		lock (_checkpointLock) {
			Interlocked.Exchange(ref StreamPosition, recordedEvent.EventNumber);
			_allPosition = recordedEvent.Position;
			_versioned = true;
		}
	}

	/// <inheritdoc cref="IListener.SeedAllPosition"/>
	public void SeedAllPosition(Position? position) {
		lock (_checkpointLock) { _allPosition = position; }
	}

	/// <summary>
	/// Serializes publishing an event with recording it. The store already delivers to one
	/// subscription sequentially, so in the steady state this is uncontended: it exists so that a
	/// holder can be sure no delivery is caught between its publish and its record, where the
	/// checkpoint does not yet name an event a subscriber already has.
	/// </summary>
	protected readonly object DeliveryLock = new();

	/// <inheritdoc cref="IListener.HoldDelivery"/>
	public IDisposable HoldDelivery() => new DeliveryHold(this);

	/// <summary>A held delivery lock, released once, by the thread that took it.</summary>
	/// <remarks>
	/// Deliberately not a <see cref="Disposer"/>. That swallows whatever its dispose function throws,
	/// so a release from the wrong thread would report success and leave delivery held for the life of
	/// the listener — the one failure here that nothing downstream could detect.
	/// </remarks>
	private sealed class DeliveryHold : IDisposable {
		private readonly object _lock;
		private readonly int _heldBy = Environment.CurrentManagedThreadId;
		private bool _released;

		public DeliveryHold(StreamListener listener) {
			_lock = listener.DeliveryLock;
			Monitor.Enter(_lock);
		}

		public void Dispose() {
			if (_released)
				return;
			if (Environment.CurrentManagedThreadId != _heldBy) {
				throw new SynchronizationLockException(
					$"A delivery hold must be released by the thread that took it ({_heldBy}), and this is " +
					$"thread {Environment.CurrentManagedThreadId}. The hold is still held.");
			}
			// Flagged after the release, so a throw leaves the hold usable rather than spent.
			Monitor.Exit(_lock);
			_released = true;
		}
	}
	public string StreamName { get; private set; } = string.Empty;
	public CatchUpSubscriptionSettings Settings { get; set; }

	/// <summary>
	/// For listening to generic streams 
	/// </summary>
	/// <param name="listenerName">The name of the listener. Useful for disambiguation when debugging.</param>
	/// <param name="streamStoreConnection">The event store to subscribe to.</param>
	/// <param name="streamNameBuilder">The source for correct stream names based on aggregates and events.</param>
	/// <param name="serializer">The event serializer.</param>
	/// <param name="busName">The name to use for the internal bus (helpful in debugging).</param>
	/// <param name="liveProcessingStarted"></param>
	/// <param name="subscriptionDropped"></param>
	public StreamListener(
		string listenerName,
		IStreamStoreConnection streamStoreConnection,
		IStreamNameBuilder streamNameBuilder,
		IEventSerializer serializer,
		string? busName = null,
		Action<Unit>? liveProcessingStarted = null,
		Action<SubscriptionDropReason, Exception?>? subscriptionDropped = null) {
		Bus = new InMemoryBus(busName ?? "Stream Listener");
		_streamStoreConnection = streamStoreConnection ?? throw new ArgumentNullException(nameof(streamStoreConnection));
		Settings = CatchUpSubscriptionSettings.Default;
		ListenerName = listenerName;
		_streamNameBuilder = streamNameBuilder;
		Serializer = serializer;
		_liveProcessingStarted = liveProcessingStarted;
		_subscriptionDropped = subscriptionDropped;
	}

	/// <summary>
	/// Event Stream Listener
	/// i.e. $et-[MessageType]
	/// </summary>
	/// <param name="tMessage">The type of the message to listen to.</param>
	/// <param name="checkpoint">An optional checkpoint to start from.</param>
	/// <param name="blockUntilLive">If true, does not return until the subscription has read all pre-existing
	/// events and converted to listening for new ones.</param>
	/// <param name="validateStream">If true, requires validating the stream name before starting.</param>
	/// <param name="cancelWaitToken">Cancellation token to cancel waiting if blockUntilLive is true.</param>
	public void Start(
		Type tMessage,
		long? checkpoint = null,
		bool blockUntilLive = false,
		bool validateStream = false,
		CancellationToken cancelWaitToken = default) {
		if (!tMessage.IsSubclassOf(typeof(Event))) {
			throw new ArgumentException("type must derive from ReactiveDomain.Messaging.Event", nameof(tMessage));
		}
		Start(
			_streamNameBuilder.GenerateForEventType(tMessage.Name),
			checkpoint,
			blockUntilLive,
			validateStream,
			cancelWaitToken);
	}

	/// <summary>
	/// Category Stream Listener
	/// i.e. $ce-[AggregateType]
	/// </summary>
	/// <typeparam name="TAggregate">The Aggregate type used to generate the stream name.</typeparam>
	/// <param name="checkpoint">An optional checkpoint to start from.</param>
	/// <param name="blockUntilLive">If true, does not return until the subscription has read all pre-existing
	/// events and converted to listening for new ones.</param>
	/// <param name="validateStream">If true, requires validating the stream name before starting.</param>
	/// <param name="cancelWaitToken">Cancellation token to cancel waiting if blockUntilLive is true.</param>
	public void Start<TAggregate>(
		long? checkpoint = null,
		bool blockUntilLive = false,
		bool validateStream = false,
		CancellationToken cancelWaitToken = default) where TAggregate : class, IEventSource {

		Start(
			_streamNameBuilder.GenerateForCategory(typeof(TAggregate)),
			checkpoint,
			blockUntilLive,
			validateStream,
			cancelWaitToken);
	}

	/// <summary>
	/// Aggregate Stream listener
	/// i.e. [AggregateType]-[id]
	/// </summary>
	/// <typeparam name="TAggregate">The Aggregate type used to generate the stream name.</typeparam>
	/// <param name="id">The ID of the aggregate to listen to.</param>
	/// <param name="checkpoint">An optional checkpoint to start from.</param>
	/// <param name="blockUntilLive">If true, does not return until the subscription has read all pre-existing
	/// events and converted to listening for new ones.</param>
	/// <param name="validateStream">If true, requires validating the stream name before starting.</param>
	/// <param name="cancelWaitToken">Cancellation token to cancel waiting if blockUntilLive is true.</param>
	public void Start<TAggregate>(
		Guid id,
		long? checkpoint = null,
		bool blockUntilLive = false,
		bool validateStream = false,
		CancellationToken cancelWaitToken = default) where TAggregate : class, IEventSource {
		Start(
			_streamNameBuilder.GenerateForAggregate(typeof(TAggregate), id),
			checkpoint,
			blockUntilLive,
			validateStream,
			cancelWaitToken);
	}

	/// <summary>
	/// Custom Stream name
	/// i.e. [StreamName]
	/// </summary>
	/// <param name="streamName">The name of the stream to listen to.</param>
	/// <param name="checkpoint">An optional checkpoint to start from.</param>
	/// <param name="blockUntilLive">If true, does not return until the subscription has read all pre-existing
	/// events and converted to listening for new ones.</param>
	/// <param name="validateStream">If true, requires validating the stream name before starting.</param>
	/// <param name="cancelWaitToken">Cancellation token to cancel waiting if blockUntilLive is true.</param>
	public virtual void Start(
		string streamName,
		long? checkpoint = null,
		bool blockUntilLive = false,
		bool validateStream = false,
		CancellationToken cancelWaitToken = default) {
		_liveLock.Reset();
		lock (_startLock) {
			if (_started)
				throw new InvalidOperationException("Listener already started.");
			if (validateStream && !ValidateStreamName(streamName))
				throw new ArgumentException("Stream not found.", streamName);
			// Not named here: SubscribeToStreamFrom names it under the checkpoint lock, together with
			// the version it goes with. Naming it first publishes a listener that reports no version
			// for a stream being resumed at a real one.
			_subscription =
				SubscribeToStreamFrom(
					streamName,
					checkpoint,
					eventAppeared: GotEvent,
					liveProcessingStarted: () => {
						// Under the delivery lock like an event, because it reaches subscribers the same
						// way and is counted the same way. Published outside it, it could land in a
						// subscriber's queue while a holder believed delivery was stopped — no checkpoint
						// names it, so a model that handles it would hold state its checkpoints disown.
						lock (DeliveryLock) {
							Bus.Publish(new StreamStoreMsgs.CatchupSubscriptionBecameLive());
						}
						_liveLock.Set();
						_liveProcessingStarted?.Invoke(Unit.Default);
					});
			_started = true;
		}
		if (blockUntilLive) {
			_liveLock.Wait(cancelWaitToken);
		}
	}
	public IDisposable SubscribeToStreamFrom(
		string stream,
		long? lastCheckpoint,
		Action<RecordedEvent> eventAppeared,
		Action? liveProcessingStarted = null,
		Action<SubscriptionDropReason, Exception?>? subscriptionDropped = null,
		UserCredentials? userCredentials = null) {
		// Named and positioned together: naming is what makes this listener checkpointable, so a
		// reader must not see the name before the version it goes with.
		lock (_checkpointLock) {
			StreamName = stream;
			// A resume point is a version this stream reached; the zero standing in for "no resume
			// point" is not, and must not be checkpointed as one.
			Interlocked.Exchange(ref StreamPosition, lastCheckpoint ?? 0);
			_versioned = lastCheckpoint.HasValue;
		}
		var sub = _streamStoreConnection.SubscribeToStreamFrom(
			stream,
			lastCheckpoint,
			Settings,
			eventAppeared,
			_ => liveProcessingStarted?.Invoke(),
			Dropped,
			userCredentials);

		return new Disposer(() => { sub.Dispose(); return Unit.Default; });

		void Dropped(SubscriptionDropReason r, Exception? e) {
			_liveLock.Set();
			(subscriptionDropped ?? _subscriptionDropped)?.Invoke(r, e);
		}
	}

	public bool ValidateStreamName(string streamName) {
		try {
			var result = _streamStoreConnection.ReadStreamForward(streamName, 0, 1);

			return result?.GetType() == typeof(StreamEventsSlice);
		} catch (Exception) {
			return false;
		}
	}

	protected virtual void GotEvent(RecordedEvent recordedEvent) {
		lock (DeliveryLock) {
			if (Serializer.Deserialize(recordedEvent) is IMessage @event) {
				Bus.Publish(@event);
			}
			// After the publish, and under the same lock. The bus hands the event to the subscriber's
			// queue synchronously, so once this runs the event is queued; recording first left a window
			// where the checkpoint named an event that had not been handed on at all, and recording
			// outside the lock leaves one where a subscriber has an event the checkpoint disowns.
			RecordDelivered(recordedEvent);
		}
	}

	#region Implementation of IDisposable

	private bool _disposed;
	public void Dispose() {
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing) {
		if (_disposed)
			return;
		_liveLock.Set();
		_subscription?.Dispose();
		Bus.Dispose();
		_disposed = true;
	}

	#endregion
}
