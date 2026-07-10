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
			StreamName = streamName;
			_subscription =
				SubscribeToStreamFrom(
					streamName,
					checkpoint,
					eventAppeared: GotEvent,
					liveProcessingStarted: () => {
						Bus.Publish(new StreamStoreMsgs.CatchupSubscriptionBecameLive());
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
		StreamName = stream;

		Interlocked.Exchange(ref StreamPosition, lastCheckpoint ?? 0);
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
		Interlocked.Exchange(ref StreamPosition, recordedEvent.EventNumber);
		if (Serializer.Deserialize(recordedEvent) is IMessage @event) {
			Bus.Publish(@event);
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
