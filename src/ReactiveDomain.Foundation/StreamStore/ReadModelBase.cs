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

	// Captures in flight, guarded by _liveLock along with _pendingStreams: a start and a capture
	// decide against each other, so they must decide under one lock or both can pass.
	private int _capturing;

	// Bumped whenever the outstanding streams are abandoned wholesale, so a sentinel queued before
	// that cannot be counted against the streams started after it.
	private int _generation;

	/// <summary>
	/// Records a stream as outstanding, arming a fresh task if none were.
	/// Called synchronously from every Start overload, so a caller that reads
	/// <see cref="IsLive"/> after starting cannot see the previous, completed task.
	/// </summary>
	/// <exception cref="InvalidOperationException">A capture is in flight.</exception>
	private int RegisterStream() {
		lock (_liveLock) {
			if (_capturing > 0) {
				throw new InvalidOperationException(
					$"{GetType().Name} is being captured, so a stream cannot be started: its read would " +
					"deliver events into the model ahead of the cut being captured, and no checkpoint " +
					"would name them. Await the capture, then start the stream.");
			}
			if (_pendingStreams == 0)
				_live = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			_pendingStreams++;
			return _generation;
		}
	}

	/// <summary>
	/// Retires one outstanding stream; completes the armed task when the last one drains.
	/// </summary>
	private void RetireStream(int generation) {
		TaskCompletionSource? drained = null;
		lock (_liveLock) {
			// A sentinel outlives the streams it was queued alongside when one of them fails, and the
			// count it would decrement by then belongs to whatever started next. Stamping it keeps it
			// from retiring a stream it never described.
			if (generation != _generation || _pendingStreams == 0)
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
			_generation++; // sentinels already queued describe streams that are no longer outstanding
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
		var generation = RegisterStream();
		try {
			if (start())
				MarkReadDrained(generation);
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
	private void MarkReadDrained(int generation) =>
		((IHandle<IMessage>)_queue).Handle(new ReadDrained(generation));

	/// <summary>
	/// Records that something else will feed this model a stream it does not read itself, so
	/// <see cref="IsLive"/> does not report live before that feed has handed over its history.
	/// </summary>
	/// <returns>
	/// The generation to hand back to <see cref="MarkExternalSourceDrained"/>. Stamping it keeps a
	/// late release from retiring a source registered after this one was abandoned.
	/// </returns>
	/// <exception cref="InvalidOperationException">A capture is in flight.</exception>
	internal int RegisterExternalSource() => RegisterStream();

	/// <summary>
	/// Queues the sentinel retiring a source registered by <see cref="RegisterExternalSource"/>. Call
	/// it after the last of that source's history has been handed over, so the sentinel goes in behind
	/// it and the target's queue folds that history first.
	/// </summary>
	/// <param name="generation">The value <see cref="RegisterExternalSource"/> returned.</param>
	internal void MarkExternalSourceDrained(int generation) => MarkReadDrained(generation);

	private sealed record ReadDrained(int Generation) : IMessage {
		public Guid MsgId { get; } = Guid.NewGuid();
	}

	/// <summary>
	/// The <see cref="RunStart"/> counterpart for the task-pool overloads. Registers before queuing
	/// the work so the registration is visible to the calling thread on return.
	/// </summary>
	private void RunStartAsync(Func<bool> start, CancellationToken cancelWaitToken) {
		var generation = RegisterStream();
		var readTask = Task.Run(() => {
			try {
				if (start())
					MarkReadDrained(generation);
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
		if (message is ReadDrained drained) {
			RetireStream(drained.Generation);
			return;
		}
		if (message is CaptureBarrier barrier) {
			RunCapture(barrier);
			return;
		}
		lock (ReaderLock) {
			_bus.Handle(message);
			Version++;
		}
	}

	private readonly List<CaptureBarrier> _captures = [];

	/// <summary>
	/// Carries the checkpoints sampled where it was enqueued, so that reaching it on the queue is
	/// proof that they describe what has been applied.
	/// </summary>
	private sealed class CaptureBarrier : IMessage {
		public Guid MsgId { get; } = Guid.NewGuid();
		public required IReadOnlyList<StreamCheckpoint> Checkpoints { get; init; }
		public required Action<IReadOnlyList<StreamCheckpoint>> Complete { get; init; }
		public required Action<Exception?> Abandon { get; init; }
	}

	/// <summary>
	/// Reads this model at a cut: <paramref name="read"/> runs against the exact state the supplied
	/// checkpoints describe, with nothing applied that they do not name.
	/// </summary>
	/// <param name="read">
	/// Reads the model's state. Runs on the queue thread under <see cref="ReaderLock"/> at the point
	/// the checkpoints describe, so it must not block, start a stream, or wait on this model.
	/// </param>
	/// <returns>Whatever <paramref name="read"/> returned, once the cut has been reached.</returns>
	/// <exception cref="InvalidOperationException">A stream is still reading, so there is no cut yet.</exception>
	/// <remarks>
	/// <para>Every listener's delivery is held while the checkpoints are sampled and a barrier is
	/// enqueued, so nothing can be published in between: everything the sample names is already ahead
	/// of the barrier, and nothing past the sample is. Reaching the barrier on the queue is therefore
	/// proof that exactly the sampled events have been applied. The hold spans two operations, not the
	/// wait — the queue drains afterwards, on its own.</para>
	/// <para>The returned task is cancelled if the model is disposed before the barrier is reached.
	/// Calling this from a handler and blocking on the result would deadlock: the barrier is behind
	/// the message being handled, on the thread that is waiting.</para>
	/// <para>Starting a stream is refused while a cut is being taken, and taking one is refused while
	/// a stream is still reading: a read in flight publishes through this model's own
	/// <see cref="Handle(IMessage)"/> rather than through a listener, so its events would be in the
	/// state with no checkpoint naming them.</para>
	/// </remarks>
	protected Task<T> ReadAtConsistentCut<T>(Func<IReadOnlyList<StreamCheckpoint>, T> read) {
		Ensure.NotNull(read, nameof(read));
		var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

		// Checked and claimed together, so a start cannot slip between them.
		lock (_liveLock) {
			if (_pendingStreams > 0) {
				throw new InvalidOperationException(
					$"{GetType().Name} has a stream still reading, so there is no cut to capture yet. " +
					"Await IsLive first.");
			}
			_capturing++;
		}

		IListener[] listeners;
		lock (_listeners) {
			listeners = _listeners.ToArray();
		}

		var holds = new List<IDisposable>(listeners.Length);
		CaptureBarrier? pending = null;
		try {
			// In list order, and nothing else takes more than one, so no two callers can take them in
			// opposite orders.
			foreach (var listener in listeners) {
				holds.Add(listener.HoldDelivery());
			}
			var barrier = new CaptureBarrier {
				Checkpoints = listeners.Select(l => l.Checkpoint).OfType<StreamCheckpoint>().ToList(),
				Complete = checkpoints => completion.TrySetResult(read(checkpoints)),
				Abandon = error => {
					if (error is null) { completion.TrySetCanceled(); } else { completion.TrySetException(error); }
				}
			};
			lock (_captures) {
				_captures.Add(pending = barrier);
			}
			((IHandle<IMessage>)_queue).Handle(barrier);
		} catch {
			// Exactly one path releases the capture, and it is whoever takes the barrier off the list.
			// Nothing can have taken it if it never went on.
			if (pending is null || Claim(pending))
				ReleaseCapture();
			throw;
		} finally {
			for (var i = holds.Count - 1; i >= 0; i--) {
				holds[i].Dispose();
			}
		}
		// A queue already stopped, or on its way there, will never dequeue the barrier.
		if (_closing)
			AbandonCaptures(null);
		return completion.Task;
	}

	private bool Claim(CaptureBarrier barrier) {
		lock (_captures) {
			return _captures.Remove(barrier);
		}
	}

	// Taken under _liveLock alone and never nested inside _captures, so the two have no order to get
	// wrong.
	private void ReleaseCapture() {
		lock (_liveLock) {
			_capturing--;
		}
	}

	private void RunCapture(CaptureBarrier barrier) {
		if (!Claim(barrier))
			return; // already abandoned
		ReleaseCapture();
		try {
			lock (ReaderLock) {
				barrier.Complete(barrier.Checkpoints);
			}
		} catch (Exception ex) {
			barrier.Abandon(ex);
		}
	}

	private void AbandonCaptures(Exception? error) {
		CaptureBarrier[] outstanding;
		lock (_captures) {
			outstanding = _captures.ToArray();
			_captures.Clear();
		}
		foreach (var barrier in outstanding) {
			ReleaseCapture();
			barrier.Abandon(error);
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

	/// <summary>How far each stream this model listens to has been delivered to it.</summary>
	/// <returns>One checkpoint per started listener.</returns>
	/// <remarks>
	/// <para><b>Delivered, not applied.</b> A listener records an event once it has handed it to this
	/// model's queue, so under live traffic this runs ahead of the state the handlers have built, by
	/// whatever is still queued. The error is one-directional and it is the dangerous direction:
	/// anything that pairs these checkpoints with a reading of the model claims events the handlers
	/// have not run yet. Read the two together with <see cref="ReadAtConsistentCut{T}"/>, or take
	/// both where nothing is in flight — after <see cref="IsLive"/> on a model whose streams are
	/// quiet, or once <see cref="Idle"/> holds and stays held. Pairing them per event needs the
	/// checkpoint to travel with the message
	/// (<a href="https://github.com/ReactiveDomain/reactive-domain/issues/211">#211</a>).</para>
	/// <para><see cref="StreamCheckpoint.Version"/> is null for a stream that has delivered nothing,
	/// and <see cref="StreamCheckpoint.Position"/> is null for any stream whose last delivered event
	/// carried no <c>$all</c> position, which is what a store that does not report one produces.</para>
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
	/// among the last events delivered from its streams.
	/// </summary>
	/// <remarks>
	/// <para><b>Not a completeness claim.</b> The model only ever saw events on its own streams, so it
	/// has not seen everything below this position. To gate a read on freshness use
	/// <see cref="LowestAppliedPosition"/>.</para>
	/// <para>Sources reporting no position are skipped rather than suppressing the answer: a greatest
	/// position over some of them is still a position this model reached, and leaving one out can only
	/// understate the reach. Null when no source reports one at all.</para>
	/// <para>Delivered, not applied — see <see cref="GetCheckpoint"/>.</para>
	/// <para><b>Not a way to compare models.</b> See <see cref="LowestAppliedPosition"/>.</para>
	/// </remarks>
	public Position? HighWaterMark {
		get {
			lock (_listeners) {
				Position? furthest = null;
				foreach (var listener in _listeners) {
					if (listener.Checkpoint?.Position is not { } position)
						continue;
					if (furthest is not { } current || position > current)
						furthest = position;
				}
				return furthest;
			}
		}
	}

	/// <summary>
	/// The position through which every one of this model's streams has been delivered — the least
	/// position among the last events delivered from them.
	/// </summary>
	/// <remarks>
	/// <para>This is the freshness signal to gate a read on: every source has handed over everything it
	/// had up to here.</para>
	/// <para>Null when the model has no listeners, or when any one of them reports no position. Unlike
	/// <see cref="HighWaterMark"/> this one cannot skip a source: a least position over some of them
	/// claims coverage for the ones left out, which is the overstatement this signal exists to
	/// avoid.</para>
	/// <para>Delivered, not applied — see <see cref="GetCheckpoint"/>. This is a lower bound on reach,
	/// not proof of application, so a reader gated on it can still be one queue depth early.</para>
	/// <para><b>A freshness reading, not a state comparison.</b> Two models that have applied the very
	/// same events report different watermarks when they read them by different routes: a projected
	/// stream's link entry sits at its own place in <c>$all</c>, later than the event it points at, so
	/// a category-fed model reports a greater position than a stream-fed one at the identical state.
	/// The skew is small and bounded, which is why this still answers "how far behind is this model",
	/// but equal watermarks are not equal states and a greater one is not a later one. To order what a
	/// model has applied, compare the checkpoints — <see cref="StreamCheckpoint.Compare"/>, which is
	/// per stream and can say <see cref="CheckpointOrder.Concurrent"/> where a single position cannot.
	/// </para>
	/// </remarks>
	public Position? LowestAppliedPosition {
		get {
			lock (_listeners) {
				if (_listeners.Count == 0)
					return null;
				Position? nearest = null;
				foreach (var listener in _listeners) {
					if (listener.Checkpoint?.Position is not { } position)
						return null;
					if (nearest is not { } current || position < current)
						nearest = position;
				}
				return nearest;
			}
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
	private volatile bool _closing;

	/// <summary>
	/// Stops message intake and processing: disposes the listeners, then joins the queue
	/// thread. Runs ahead of the virtual dispose chain (which tears down derived state)
	/// so that no handler can be dispatched into state a derived class has already
	/// disposed. Idempotent.
	/// </summary>
	private void StopMessagePump() {
		// Set before anything is torn down, so a capture registered at any point from here on abandons
		// itself. Until now this held only because Dispose happens to run this method twice.
		_closing = true;
		lock (_listeners) {
			_listeners.ForEach(l => l.Dispose());
		}

		_queue.Stop();
		// The queue is stopped, so a sentinel still in it will never be dequeued; release anyone
		// awaiting IsLive rather than leave them on a stream that can no longer drain.
		RetireAllStreams(null);
		// Same for a capture waiting on a marker that can no longer arrive. After the listeners are
		// disposed, so a hold this releases cannot be retaken.
		AbandonCaptures(null);
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
	public virtual void DirectApply(IMessage message) {
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
	public virtual void Publish(IMessage message) {
		((IPublisher)_queue).Publish(message);
	}
}
