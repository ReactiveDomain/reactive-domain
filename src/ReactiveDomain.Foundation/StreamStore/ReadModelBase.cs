using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Util;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation;

public abstract class ReadModelBase :
	IHandle<IMessage>,
	IHandle<Message>,
	IPublisher,
	IMessageRegistry,
	IDisposable {
	private readonly Func<IListener> _getListener;
	private readonly List<IListener> _listeners;
	private readonly Func<IStreamReader> _getReader;
	private readonly InMemoryBus _bus;
	private readonly QueuedHandler _queue;
	public int MessageCount => _queue.MessageCount;
	public bool Idle => _queue.Idle;

	// Readers complete when the queue has caught up — or when the model is disposed, so a
	// dispose mid-read cannot leave the reader spinning against a stopped queue.
	private bool ReadCompleted => Idle || _disposed;

	/// <summary>
	/// ReaderLock locks the event handler and can be used when reading the model 
	/// to ensure model state is unchanged during read.
	/// The lock should *not* be used in Handle methods as they are inside the lock already by default.
	/// </summary>
	protected readonly object ReaderLock = new();

	/// <summary>
	/// The version is equal to the number of messages passed to the read model.
	/// The version is incremented after all handlers have been processed.
	/// The number of handlers (including none) will not impact the version.
	/// This can be used to ensure read model state for tests. This is *not*
	/// the same as the version of any particular stream being read. This can
	/// include <see cref="StreamStoreMsgs.CatchupSubscriptionBecameLive"/>,
	/// which may result in the Version being 1 greater than otherwise expected.
	/// </summary>
	public int Version { get; private set; }

	private readonly object _liveLock = new();
	private int _pendingStreams;
	private TaskCompletionSource _live = AlreadyLive();

	/// <summary>
	/// Gets a task that completes when every stream started on this model has <b>dispatched</b> its
	/// last historical event through the model's handlers — the model is live <i>and</i> populated.
	/// </summary>
	/// <remarks>
	/// <para><b>Timing contract:</b> a started stream is satisfied when a sentinel queued after its
	/// read is dequeued. The sentinel sits behind everything the read delivered, so dequeuing it
	/// proves those events were handled. Awaiting this task is therefore enough to read the model:
	/// there is no window in which it reports live over an empty or stale one. Events appended after
	/// the read are live traffic, not history, and are not waited for.</para>
	/// <para><b>Compositional:</b> one task spans every stream started with any <c>Start</c> or
	/// <c>StartAsync</c> overload — the synchronous ones included — and completes only when all of
	/// them have drained. A model with nothing started is vacuously live.</para>
	/// <para><b>Re-arming and snapshot semantics:</b> the value is the task that was armed when the
	/// property was read. Starting a stream while none are outstanding arms a fresh task, so a
	/// <c>Start</c> issued after an earlier <c>await</c> completed <i>is</i> represented — by the next
	/// read of the property. A task already handed out never "un-completes". Always write
	/// <c>Start…(); await rm.IsLive;</c> rather than caching the task across starts.</para>
	/// <para>The task faults if a start path throws before its listener is attached, and is cancelled
	/// if the model is disposed with streams still outstanding, so an awaiting caller is never left
	/// on a stream that can no longer drain.</para>
	/// <para><b>Out of scope — subscription lifecycle.</b> Nothing a subscription does can stall or
	/// falsely complete this task; ordering rests on this model's own queue alone. A subscription
	/// that drops is today neither reported nor retried
	/// (<a href="https://github.com/ReactiveDomain/reactive-domain/issues/267">#267</a>: reconnect
	/// from the listener's position, and throw if the reconnect fails). Do not read this task as a
	/// health signal — it says the model went live, not that it still is.</para>
	/// <para>A barrier over events committed after the read is
	/// <see cref="CatchUpConnection.WaitForCatchUp"/>.</para>
	/// </remarks>
	public Task IsLive {
		get {
			lock (_liveLock) {
				return _live.Task;
			}
		}
	}

	private static TaskCompletionSource AlreadyLive() {
		// A model with nothing started is vacuously live.
		var source = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		source.SetResult();
		return source;
	}

	/// <summary>
	/// Records a stream as outstanding, arming a fresh task if none were.
	/// Called synchronously from every Start overload, so a caller that reads
	/// <see cref="IsLive"/> after starting cannot see the previous, completed task.
	/// </summary>
	private void RegisterStream() {
		lock (_liveLock) {
			if (_pendingStreams == 0)
				_live = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			_pendingStreams++;
		}
	}

	/// <summary>
	/// Retires one outstanding stream; completes the armed task when the last one drains.
	/// </summary>
	private void RetireStream() {
		TaskCompletionSource? drained = null;
		lock (_liveLock) {
			if (_pendingStreams == 0)
				return;
			if (--_pendingStreams == 0)
				drained = _live;
		}
		// Capture under the lock, signal outside it: an awaiter released here may take _liveLock.
		drained?.TrySetResult();
	}

	/// <summary>
	/// Retires every outstanding stream without completing normally. Used when a start path can no
	/// longer drain: <paramref name="error"/> faults the armed task, otherwise it is
	/// cancelled. Never completes it successfully — a stream that did not drain must not be reported
	/// as live.
	/// </summary>
	private void RetireAllStreams(Exception? error) {
		TaskCompletionSource? armed = null;
		lock (_liveLock) {
			if (_pendingStreams == 0)
				return;
			_pendingStreams = 0;
			armed = _live;
		}
		// Signalled outside the lock, as in RetireStream.
		if (error is null)
			armed.TrySetCanceled();
		else
			armed.TrySetException(error);
	}

	/// <summary>
	/// Runs a start body under liveness tracking. Queues the retiring sentinel once the body returns.
	/// A body returning false did not attach a listener (the model was disposed mid-read).
	/// </summary>
	private void RunStart(Func<bool> start) {
		RegisterStream();
		try {
			if (start())
				MarkReadDrained();
			else
				RetireAllStreams(null);
		} catch (Exception ex) {
			RetireAllStreams(ex);
			throw;
		}
	}

	/// <summary>
	/// Queues the sentinel that retires a stream. It goes in behind everything the read delivered, so
	/// dequeuing it proves those were handled — which the reader's own completion check cannot,
	/// because that check reads the queue's starving flag and can see it set before the queue thread
	/// has picked up the work just enqueued.
	/// </summary>
	private void MarkReadDrained() => ((IHandle<IMessage>)_queue).Handle(new ReadDrained());

	private sealed record ReadDrained : IMessage {
		public Guid MsgId { get; } = Guid.NewGuid();
	}

	/// <summary>
	/// The <see cref="RunStart"/> counterpart for the task-pool overloads. Registers before queuing
	/// the work so the registration is visible to the calling thread on return.
	/// </summary>
	private void RunStartAsync(Func<bool> start, CancellationToken cancelWaitToken) {
		RegisterStream();
		var readTask = Task.Run(() => {
			try {
				if (start())
					MarkReadDrained();
				else
					RetireAllStreams(null);
			} catch (Exception ex) {
				RetireAllStreams(ex);
				throw;
			}
		}, cancelWaitToken);
		// Nothing awaits this task: a fault has already been reported through IsLive by the body's
		// catch, so the continuation only has to observe it. A token already cancelled means the
		// body never ran, so nothing else would retire the stream.
		_ = readTask.ContinueWith(t => {
			if (t.IsCanceled)
				RetireAllStreams(null);
			else
				_ = t.Exception;
		}, TaskContinuationOptions.ExecuteSynchronously);
	}

	/// <summary>
	/// Creates a read model using the provided stream store connection. Reads existing events using a
	/// reader, then transitions to a listener for live events.
	/// </summary>
	/// <param name="name">The name of the read model. Also used as the names of the listener and reader.</param>
	/// <param name="connection">A connection to a stream store.</param>
	protected ReadModelBase(string name, IConfiguredConnection connection) {
		Ensure.NotNull(connection, nameof(connection));
		_getReader = () => connection.GetReader(name, Handle);
		_getListener = () => connection.GetListener(name);
		_listeners = [];
		_bus = new InMemoryBus($"{nameof(ReadModelBase)}:{name} bus", false);
		_queue = new QueuedHandler(new AdHocHandler<IMessage>(DequeueMessage),
			$"{nameof(ReadModelBase)}:{name} queue");
		_queue.Start();
	}

	/// <summary>
	/// Every message handled by the read model will pass through here.
	/// </summary>
	private void DequeueMessage(IMessage message) {
		// Not published to handlers and not counted: it is this model's own bookkeeping, not an event.
		if (message is ReadDrained) {
			RetireStream();
			return;
		}
		lock (ReaderLock) {
			_bus.Handle(message);
			Version++;
		}
	}

	/// <summary>Creates a listener, seeds its <c>$all</c> position, and feeds it into this model's queue.</summary>
	/// <param name="readAllPosition">
	/// Where the reader that just replayed this stream's history left off, so a checkpoint taken
	/// before the first live event still accounts for what the reader applied.
	/// </param>
	private IListener AddNewListener(Position? readAllPosition) {
		var l = _getListener();
		lock (_listeners) {
			_listeners.Add(l);
		}

		l.SeedAllPosition(readAllPosition);
		l.EventStream.SubscribeToAll(_queue);
		return l;
	}

	/// <summary>How far this model has applied each stream it listens to.</summary>
	/// <returns>One checkpoint per started listener.</returns>
	/// <remarks>
	/// <see cref="StreamCheckpoint.Position"/> is null for any stream whose last delivered event
	/// carried no <c>$all</c> position, which is what a store that does not report one produces.
	/// </remarks>
	public List<StreamCheckpoint> GetCheckpoint() {
		lock (_listeners) {
			// A listener is in this list from the moment it is created, but is only checkpointable
			// once started — until then it has no stream name to report.
			return _listeners.Select(l => l.Checkpoint).OfType<StreamCheckpoint>().ToList();
		}
	}

	/// <summary>
	/// The furthest into the store's <c>$all</c> log this model has reached — the greatest position
	/// among the last events applied from its streams.
	/// </summary>
	/// <remarks>
	/// <para><b>Not a completeness claim.</b> The model only ever saw events on its own streams, so it
	/// has not seen everything below this position. To gate a read on freshness use
	/// <see cref="LowestAppliedPosition"/>.</para>
	/// <para>Null when the model has no listeners, or when any one of them reports no position —
	/// projecting over the rest would silently overstate.</para>
	/// </remarks>
	public Position? HighWaterMark => ProjectAllPositions(takeGreatest: true);

	/// <summary>
	/// The position through which every one of this model's streams has been applied — the least
	/// position among the last events applied from them.
	/// </summary>
	/// <remarks>
	/// <para>This is the honest freshness signal: everything each source had delivered up to here has
	/// been handled. Compare against it to gate a read.</para>
	/// <para>Null when the model has no listeners, or when any one of them reports no position —
	/// projecting over the rest would silently understate which sources are covered.</para>
	/// </remarks>
	public Position? LowestAppliedPosition => ProjectAllPositions(takeGreatest: false);

	private Position? ProjectAllPositions(bool takeGreatest) {
		lock (_listeners) {
			if (_listeners.Count == 0)
				return null;
			Position? projected = null;
			foreach (var listener in _listeners) {
				if (listener.Checkpoint?.Position is not { } position)
					return null;
				if (projected is not { } current) {
					projected = position;
					continue;
				}
				if (takeGreatest ? position > current : position < current)
					projected = position;
			}
			return projected;
		}
	}

	/// <summary>
	/// The stream of events that handlers should subscribe to.
	/// </summary>
	public ISubscriber EventStream => _bus;

	/// <inheritdoc cref="IMessageRegistry.RegisteredMessageTypes"/>
	/// <remarks>Registrations reach this only through <see cref="EventStream"/>.</remarks>
	public IReadOnlyCollection<Type> RegisteredMessageTypes => _bus.RegisteredMessageTypes;

	/// <inheritdoc cref="IMessageRegistry.HandledMessageTypes"/>
	/// <remarks>
	/// The types this model's handlers receive. It says nothing about which streams it listens to:
	/// a listener feeds the queue whatever its stream carries, and the types nothing handles are
	/// dropped here rather than at the listener.
	/// </remarks>
	public IReadOnlyCollection<Type> HandledMessageTypes => _bus.HandledMessageTypes;

	/// <summary>
	/// Start playback of a named stream.
	/// </summary>
	/// <param name="stream">The name of the stream to play back.</param>
	/// <param name="checkpoint">The event to start with.</param>
	/// <param name="blockUntilLive">If true, blocks returning from this method until the listener has caught up.
	/// <br/>
	/// <b>This parameter is deprecated and will be removed in a future release. Use <see cref="StartAsync"/> and
	/// await <see cref="IsLive"/> instead.</b></param>
	/// <param name="validateStream">ensure the stream exists on start</param>
	/// <param name="cancelWaitToken">Cancellation token to cancel waiting if blockUntilLive is true.</param>
	public void Start(string stream, long? checkpoint = null, bool blockUntilLive = false,
		bool validateStream = false, CancellationToken cancelWaitToken = default) {
		RunStart(() => {
			using var reader = _getReader();
			reader.Read(stream, () => ReadCompleted, checkpoint);
			if (_disposed)
				return false;
			// One read of the reader: version and position must come from the same event.
			var read = reader.Checkpoint;
			var position = read?.Version ?? checkpoint;

			AddNewListener(read?.Position).Start(stream, position, blockUntilLive, validateStream, cancelWaitToken);
			return true;
		});
	}

	/// <summary>
	/// Start playback of a named stream on a task pool thread.
	/// Await <see cref="IsLive"/> to know when every started stream has been read and folded into
	/// the model.
	/// </summary>
	/// <param name="stream">The name of the stream to play back.</param>
	/// <param name="checkpoint">The event to start with.</param>
	/// <param name="validateStream">ensure the stream exists on start</param>
	/// <param name="cancelWaitToken">Cancellation token to cancel waiting if blockUntilLive is true.</param>
	public void StartAsync(string stream, long? checkpoint = null, bool validateStream = false,
		CancellationToken cancelWaitToken = default) {
		RunStartAsync(() => {
			using var reader = _getReader();
			reader.Read(stream, () => ReadCompleted, checkpoint);
			if (_disposed)
				return false;
			// One read of the reader: version and position must come from the same event.
			var read = reader.Checkpoint;
			var position = read?.Version ?? checkpoint;

			AddNewListener(read?.Position).Start(stream, position, false, validateStream, cancelWaitToken);
			return true;
		}, cancelWaitToken);
	}

	/// <summary>
	/// Start playback of a specific stream of type <typeparamref name="TAggregate"/>.
	/// </summary>
	/// <typeparam name="TAggregate">The type of stream to play back.</typeparam>
	/// <param name="id">The ID of the stream to play back.</param>
	/// <param name="checkpoint">The event to start with.</param>
	/// <param name="blockUntilLive">If true, blocks returning from this method until the listener has caught up.
	/// <br/>
	/// <b>This parameter is deprecated and will be removed in a future release. Use
	/// <see cref="StartAsync{TAggregate}(System.Guid,long?,bool,System.Threading.CancellationToken)"/> and
	/// await <see cref="IsLive"/> instead.</b></param>
	/// <param name="validateStream">ensure the stream exists on start</param>
	/// <param name="cancelWaitToken">Cancellation token to cancel waiting if blockUntilLive is true.</param>
	public void Start<TAggregate>(Guid id, long? checkpoint = null, bool blockUntilLive = false,
		bool validateStream = false, CancellationToken cancelWaitToken = default)
		where TAggregate : class, IEventSource {
		RunStart(() => {
			using var reader = _getReader();
			reader.Read<TAggregate>(id, () => ReadCompleted, checkpoint);
			if (_disposed)
				return false;
			// One read of the reader: version and position must come from the same event.
			var read = reader.Checkpoint;
			var position = read?.Version;

			AddNewListener(read?.Position).Start<TAggregate>(id, position, blockUntilLive, validateStream, cancelWaitToken);
			return true;
		});
	}

	/// <summary>
	/// Start playback of a specific stream of type <typeparamref name="TAggregate"/> on a task pool thread.
	/// Await <see cref="IsLive"/> to know when every started stream has been read and folded into
	/// the model.
	/// </summary>
	/// <typeparam name="TAggregate">The type of stream to play back.</typeparam>
	/// <param name="id">The ID of the stream to play back.</param>
	/// <param name="checkpoint">The event to start with.</param>
	/// <param name="validateStream">ensure the stream exists on start</param>
	/// <param name="cancelWaitToken">Cancellation token to cancel waiting if blockUntilLive is true.</param>
	public void StartAsync<TAggregate>(Guid id, long? checkpoint = null, bool validateStream = false,
		CancellationToken cancelWaitToken = default) where TAggregate : class, IEventSource {
		RunStartAsync(() => {
			using var reader = _getReader();
			reader.Read<TAggregate>(id, () => ReadCompleted, checkpoint);
			if (_disposed)
				return false;
			// One read of the reader: version and position must come from the same event.
			var read = reader.Checkpoint;
			var position = read?.Version;

			AddNewListener(read?.Position).Start<TAggregate>(id, position, false, validateStream, cancelWaitToken);
			return true;
		}, cancelWaitToken);
	}

	/// <summary>
	/// Start a category listener for type <typeparamref name="TAggregate"/>.
	/// </summary>
	/// <typeparam name="TAggregate">The type of stream to play back.</typeparam>
	/// <param name="checkpoint">The event to start with.</param>
	/// <param name="blockUntilLive">If true, blocks returning from this method until the listener has caught up.
	/// <br/>
	/// <b>This parameter is deprecated and will be removed in a future release. Use
	/// <see cref="StartAsync{TAggregate}(long?,bool,System.Threading.CancellationToken)"/> and await
	/// <see cref="IsLive"/> instead.</b></param>
	/// <param name="validateStream">ensure the stream exists on start</param>
	/// <param name="cancelWaitToken">Cancellation token to cancel waiting if blockUntilLive is true.</param>
	public void Start<TAggregate>(long? checkpoint = null, bool blockUntilLive = false, bool validateStream = false,
		CancellationToken cancelWaitToken = default) where TAggregate : class, IEventSource {
		RunStart(() => {
			using var reader = _getReader();
			reader.Read<TAggregate>(() => ReadCompleted, checkpoint);
			if (_disposed)
				return false;
			// One read of the reader: version and position must come from the same event.
			var read = reader.Checkpoint;
			var position = read?.Version;

			AddNewListener(read?.Position).Start<TAggregate>(position, blockUntilLive, validateStream, cancelWaitToken);
			return true;
		});
	}

	/// <summary>
	/// Start a category listener for type <typeparamref name="TAggregate"/>.
	/// Events are played back on a task pool thread.
	/// Await <see cref="IsLive"/> to know when every started stream has been read and folded into
	/// the model.
	/// </summary>
	/// <typeparam name="TAggregate">The type of stream to play back.</typeparam>
	/// <param name="checkpoint">The event to start with.</param>
	/// <param name="validateStream">ensure the stream exists on start</param>
	/// <param name="cancelWaitToken">Cancellation token to cancel waiting if blockUntilLive is true.</param>
	public void StartAsync<TAggregate>(long? checkpoint = null, bool validateStream = false,
		CancellationToken cancelWaitToken = default) where TAggregate : class, IEventSource {
		RunStartAsync(() => {
			using var reader = _getReader();
			reader.Read<TAggregate>(() => ReadCompleted, checkpoint);
			if (_disposed)
				return false;
			// One read of the reader: version and position must come from the same event.
			var read = reader.Checkpoint;
			var position = read?.Version;

			AddNewListener(read?.Position).Start<TAggregate>(position, false, validateStream, cancelWaitToken);
			return true;
		}, cancelWaitToken);
	}

	/// <summary>
	/// Dispose of resources.
	/// </summary>
	public void Dispose() {
		StopMessagePump();
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	private bool _disposed;

	/// <summary>
	/// Stops message intake and processing: disposes the listeners, then joins the queue
	/// thread. Runs ahead of the virtual dispose chain (which tears down derived state)
	/// so that no handler can be dispatched into state a derived class has already
	/// disposed. Idempotent.
	/// </summary>
	private void StopMessagePump() {
		lock (_listeners) {
			_listeners.ForEach(l => l.Dispose());
		}

		_queue.Stop();
		// The queue is stopped, so a sentinel still in it will never be dequeued; release anyone
		// awaiting IsLive rather than leave them on a stream that can no longer drain.
		RetireAllStreams(null);
	}

	protected virtual void Dispose(bool disposing) {
		if (_disposed)
			return;
		if (disposing) {
			StopMessagePump();
			_bus.Dispose();
		}

		_disposed = true;
	}

	/// <summary>
	/// Applies a message synchronously to the read model while ensuring that the <see cref="ReaderLock"/>
	/// is respected and bypasses both the queue and listeners. This is primarily useful in tests.
	/// </summary>
	/// <param name="message">The message to apply.</param>
	public void DirectApply(IMessage message) {
		DequeueMessage(message);
	}

	public void Handle(Message message) {
		((IHandle<IMessage>)_queue).Handle(message);
	}

	public void Handle(IMessage message) {
		((IHandle<IMessage>)_queue).Handle(message);
	}

	/// <summary>
	/// Publishes a message onto the read model's internal queue.
	/// This bypasses the Listeners while ensuring that the <see cref="ReaderLock"/>
	/// is respected. All messages will be processed in order from the queue thread.
	/// </summary>
	/// <param name="message">The message to publish.</param>
	public void Publish(IMessage message) {
		((IPublisher)_queue).Publish(message);
	}
}
