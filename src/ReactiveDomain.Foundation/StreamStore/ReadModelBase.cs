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
	private readonly IStreamNameBuilder _namer;
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
	/// the same as the version of any particular stream being read.
	/// <see cref="StreamStoreMsgs.CatchupSubscriptionBecameLive"/> is dispatched to handlers but not
	/// counted: it is a listener's transition, not a message from a stream.
	/// </summary>
	public int Version { get; private set; }

	private readonly object _liveLock = new();
	private readonly TaskCompletionSource _subscriptionsLost =
		new(TaskCreationOptions.RunContinuationsAsynchronously);

	/// <summary>
	/// Faults when a live subscription drops and cannot be resumed. Completes when the model is
	/// disposed. Not <see cref="IsLive"/> — that is the read-to-live transition and stays completed.
	/// </summary>
	public Task Subscriptions => _subscriptionsLost.Task;
	private int _pendingStreams;
	private long _registrations;
	private TaskCompletionSource _live = AlreadyLive();

	// Callbacks waiting for the live transition (Queued false) or already on the queue (Queued
	// true). Guarded by _liveLock, with _pendingStreams: which of the two a registration becomes is
	// decided against the count, so it must be decided under the same lock.
	private readonly List<LiveCallback> _liveCallbacks = [];

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
	/// <c>Start…(); await rm.IsLive;</c> rather than caching the task across starts — or let
	/// <see cref="StartAllAsync"/> do the sequencing, which is what it is for.</para>
	/// <para>Continuations run off the queue thread and race the next <c>Handle</c>. To run something
	/// <i>at</i> the transition, sequenced with the handlers, use <see cref="OnceLive"/>.</para>
	/// <para>The task faults if a start path throws before its listener is attached, or if the model's
	/// own live transition fails — see <see cref="BufferedReadModelBase"/> for what that means there.
	/// It is cancelled if the model is disposed with the transition still outstanding, so an awaiting
	/// caller is never left on a stream that can no longer drain.</para>
	/// <para><b>Out of scope — subscription lifecycle.</b> Nothing a subscription does can stall or
	/// falsely complete this task; ordering rests on this model's own queue alone. A live subscription
	/// that drops is resumed from the listener's last version; if the reconnect cannot be made,
	/// <see cref="Subscriptions"/> faults. Do not read this task as a health signal — it says the
	/// model <b>went</b> live, not that it <b>still is</b>.</para>
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
	/// <param name="externalSource">True when the registration is for a source attached with <see cref="RelayTo"/>, false when it is for a stream this model reads.</param>
	/// <exception cref="InvalidOperationException">A capture is in flight.</exception>
	/// <exception cref="ObjectDisposedException">The model is closing or closed.</exception>
	private int RegisterStream(bool externalSource = false) {
		int generation;
		ModelRelay[]? awaiting = null;
		lock (_liveLock) {
			// Under the lock, against the flag StopMessagePump sets before it retires what is
			// outstanding: a registration made after that retire would arm a task nothing completes.
			ObjectDisposedException.ThrowIf(_closing, this);
			if (_capturing > 0) {
				throw new InvalidOperationException(externalSource
					? $"{GetType().Name} is being captured, so a source cannot be attached: what it hands over " +
					  "would reach the model ahead of the cut being captured, and no checkpoint would name " +
					  "it. Await the capture, then attach."
					: $"{GetType().Name} is being captured, so a stream cannot be started: its read would " +
					  "deliver events into the model ahead of the cut being captured, and no checkpoint " +
					  "would name them. Await the capture, then start the stream.");
			}
			if (_pendingStreams == 0 && _live.Task.IsCompleted)
				_live = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
			_pendingStreams++;
			_registrations++;
			generation = _generation;
			if (_relaysAwaitingStart.Count > 0) {
				awaiting = _relaysAwaitingStart.ToArray();
				_relaysAwaitingStart.Clear();
			}
		}
		// Outside the lock, and with this registration counted, so the release each relay arms waits
		// for the transition rather than being queued as if nothing were outstanding.
		if (awaiting is not null) {
			foreach (var relay in awaiting)
				ArmRelease(relay);
		}
		return generation;
	}

	/// <summary>
	/// Retires one outstanding stream. When the last one drains, runs the live transition on this
	/// thread — the queue's — and then completes the armed task.
	/// </summary>
	private void RetireStream(int generation) {
		lock (_liveLock) {
			// A sentinel outlives the streams it was queued alongside when one of them fails, and the
			// count it would decrement by then belongs to whatever started next. Stamping it keeps it
			// from retiring a stream it never described.
			if (generation != _generation || _pendingStreams == 0)
				return;
			if (--_pendingStreams != 0)
				return;
		}
		// Under ReaderLock and ahead of the next dequeue, so the transition is sequenced with the
		// handlers exactly as an event is. AdvanceLiveTransition returning false leaves the armed task
		// pending — a buffered model whose flush missed retries without waiting for another event.
		Exception? failure = null;
		var complete = false;
		lock (ReaderLock) {
			try {
				complete = AdvanceLiveTransition();
			} catch (Exception ex) {
				failure = ex;
			}
		}
		if (failure is not null)
			FailLiveTransition(failure);
		else if (complete)
			CompleteLiveTransition();
	}

	/// <summary>Removes and returns the callbacks still waiting for the transition. Call under <c>_liveLock</c>.</summary>
	private List<LiveCallback> TakeWaitingCallbacks() {
		var waiting = _liveCallbacks.Where(c => !c.Queued).ToList();
		_liveCallbacks.RemoveAll(c => !c.Queued);
		return waiting;
	}

	/// <summary>
	/// Runs on the queue thread, under <see cref="ReaderLock"/>, each time the last outstanding stream
	/// drains — before any <see cref="OnceLive"/> callback and before <see cref="IsLive"/> completes.
	/// A throw faults <see cref="IsLive"/>.
	/// </summary>
	internal virtual void AtLiveTransition() { }

	/// <summary>
	/// Runs the live transition. True means <see cref="IsLive"/> can complete; false means the
	/// transition is still in flight (a flush will be retried) and the armed task stays pending.
	/// </summary>
	/// <remarks>
	/// The result says whether the transition finished, not whether it succeeded: a throw is a failed
	/// transition and faults <see cref="IsLive"/>, which is what keeps a model from reporting live over
	/// a store it could not write.
	/// </remarks>
	internal virtual bool AdvanceLiveTransition() {
		AtLiveTransition();
		return true;
	}

	/// <summary>
	/// Another attempt at a transition that returned false. Queue-thread bookkeeping, not an event.
	/// </summary>
	internal virtual void RetryTransition() { }

	/// <summary>Queues <see cref="RetryTransition"/> behind whatever is being handled now.</summary>
	internal void EnqueueTransitionRetry() => Enqueue(new TransitionRetry());

	private sealed record TransitionRetry : IMessage {
		public Guid MsgId { get; } = Guid.NewGuid();
	}

	internal void CompleteLiveTransition() {
		List<LiveCallback> callbacks;
		TaskCompletionSource drained;
		lock (_liveLock) {
			if (_live.Task.IsCompleted || _pendingStreams != 0)
				return;
			drained = _live;
			callbacks = TakeWaitingCallbacks();
		}
		lock (ReaderLock) {
			foreach (var callback in callbacks)
				callback.Run();
		}
		drained.TrySetResult();
	}

	internal void FailLiveTransition(Exception error) {
		List<LiveCallback> callbacks;
		TaskCompletionSource drained;
		lock (_liveLock) {
			if (_live.Task.IsCompleted)
				return;
			drained = _live;
			callbacks = TakeWaitingCallbacks();
		}
		foreach (var callback in callbacks)
			Abandon(callback, error);
		drained.TrySetException(error);
	}

	/// <summary>
	/// Runs on the queue thread, under <see cref="ReaderLock"/>, after each message has been through
	/// the handlers.
	/// </summary>
	internal virtual void AfterDispatch() { }

	/// <summary>
	/// Runs after the listeners are disposed and the queue has drained, while the queue thread is
	/// still running, so a snapshot can be taken against applied state before the pump is joined.
	/// </summary>
	internal virtual void BeforeStopping() { }

	/// <summary>True while no started stream is still reading. Guarded by <c>_liveLock</c>.</summary>
	internal bool NoStreamsPending {
		get {
			lock (_liveLock) {
				return _pendingStreams == 0;
			}
		}
	}

	/// <summary>
	/// Retires every outstanding stream without completing normally. Used when a start path can no
	/// longer drain: <paramref name="error"/> faults the armed task, otherwise it is
	/// cancelled. Never completes it successfully — a stream that did not drain must not be reported
	/// as live.
	/// </summary>
	private void RetireAllStreams(Exception? error) {
		TaskCompletionSource? armed = null;
		List<LiveCallback>? callbacks = null;
		lock (_liveLock) {
			if (_pendingStreams == 0 && _live.Task.IsCompleted)
				return;
			_pendingStreams = 0;
			_generation++; // sentinels already queued describe streams that are no longer outstanding
			armed = _live;
			// Only those waiting for the transition: one already on the queue describes a model that
			// was live when it was registered, and still runs.
			callbacks = TakeWaitingCallbacks();
		}
		// Signalled outside the lock, as in RetireStream.
		if (error is null)
			armed.TrySetCanceled();
		else
			armed.TrySetException(error);
		foreach (var callback in callbacks) {
			Abandon(callback, error);
		}
	}

	private static void Abandon(LiveCallback callback, Exception? error) {
		if (error is null)
			callback.Completion.TrySetCanceled();
		else
			callback.Completion.TrySetException(error);
	}

	/// <summary>
	/// Runs a start body under liveness tracking. Queues the retiring sentinel once the body returns,
	/// carrying where the read left the stream. A body that finds the model disposed returns null
	/// without attaching a listener.
	/// </summary>
	private void RunStart(Func<StreamCheckpoint?> start) {
		var generation = RegisterStream();
		try {
			var read = start();
			if (read is null && _disposed)
				RetireAllStreams(null);
			else
				MarkReadDrained(generation, read);
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
	/// <param name="generation">The value <see cref="RegisterStream"/> returned for this stream.</param>
	/// <param name="read">
	/// Where the read left the stream, so <see cref="AppliedCheckpoints"/> has an entry for a stream
	/// whose read delivered nothing, which no later event would supply.
	/// </param>
	private void MarkReadDrained(int generation, StreamCheckpoint? read) =>
		Enqueue(new ReadDrained(generation, read));

	/// <summary>
	/// Records that something else will feed this model a stream it does not read itself, so
	/// <see cref="IsLive"/> does not report live before that feed has handed over its history.
	/// </summary>
	/// <returns>
	/// The generation to hand back to <see cref="MarkExternalSourceDrained"/>. Stamping it keeps a
	/// late release from retiring a source registered after this one was abandoned.
	/// </returns>
	/// <exception cref="InvalidOperationException">A capture is in flight.</exception>
	/// <exception cref="ObjectDisposedException">The model is closing or closed.</exception>
	internal int RegisterExternalSource() => RegisterStream(externalSource: true);

	/// <summary>
	/// Queues the sentinel retiring a source registered by <see cref="RegisterExternalSource"/>. Call
	/// it after the last of that source's history has been handed over, so the sentinel goes in behind
	/// it and the target's queue folds that history first.
	/// </summary>
	/// <param name="generation">The value <see cref="RegisterExternalSource"/> returned.</param>
	/// <param name="read">
	/// Where this source left the stream — the last event this target was handed, or the resume point
	/// when it was handed nothing — so <see cref="AppliedCheckpoints"/> has an entry for the stream
	/// even when no event arrived on it.
	/// </param>
	internal void MarkExternalSourceDrained(int generation, StreamCheckpoint? read) =>
		MarkReadDrained(generation, read);

	private sealed record ReadDrained(int Generation, StreamCheckpoint? Read) : IMessage {
		public Guid MsgId { get; } = Guid.NewGuid();
	}

	/// <summary>An event off a listener, with that event's checkpoint — see <see cref="IListener.SubscribeToDelivery"/>.</summary>
	private sealed record Delivered(IMessage Message, StreamCheckpoint? Checkpoint) : IMessage {
		public Guid MsgId => Message.MsgId;
	}

	/// <summary>A change a source model emitted, with that model's applied checkpoints at the emit — see <see cref="RelayTo"/>.</summary>
	private sealed record Relayed(IMessage Change, IReadOnlyList<StreamCheckpoint> Applied, ModelRelay Source) : IMessage {
		public Guid MsgId => Change.MsgId;
	}

	/// <summary>Retires a model relay's registration, carrying where its source stood when it released.</summary>
	private sealed record SourceDrained(int Generation, IReadOnlyList<StreamCheckpoint> Applied, ModelRelay Source) : IMessage {
		public Guid MsgId { get; } = Guid.NewGuid();
	}

	/// <summary>A model relay has been disposed: its source no longer bounds the streams it fed.</summary>
	private sealed record SourceDetached(ModelRelay Source) : IMessage {
		public Guid MsgId { get; } = Guid.NewGuid();
	}

	/// <summary>A callback registered through <see cref="OnceLive"/>.</summary>
	private sealed class LiveCallback : IMessage {
		public Guid MsgId { get; } = Guid.NewGuid();
		public required Action Callback { get; init; }
		public required TaskCompletionSource Completion { get; init; }
		/// <summary>Set when the model was live at registration and the callback went straight onto the queue.</summary>
		public bool Queued { get; init; }

		/// <summary>Runs the callback and settles <see cref="Completion"/> with what it did. Never throws.</summary>
		public void Run() {
			try {
				Callback();
				Completion.TrySetResult();
			} catch (Exception ex) {
				Completion.TrySetException(ex);
			}
		}
	}

	/// <summary>
	/// The <see cref="RunStart"/> counterpart for the task-pool overloads. Registers before queuing
	/// the work so the registration is visible to the calling thread on return.
	/// </summary>
	private void RunStartAsync(Func<StreamCheckpoint?> start, CancellationToken cancelWaitToken) {
		var generation = RegisterStream();
		var readTask = Task.Run(() => {
			try {
				var read = start();
				if (read is null && _disposed)
					RetireAllStreams(null);
				else
					MarkReadDrained(generation, read);
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
		_namer = connection.StreamNamer;
		_listeners = [];
		_bus = new InMemoryBus($"{nameof(ReadModelBase)}:{name} bus", false);
		_queue = new QueuedHandler(new AdHocHandler<IMessage>(DequeueMessage),
			$"{nameof(ReadModelBase)}:{name} queue");
		_queue.Start();
		_getReader = () => {
			// Paired like a listener's delivery, so AppliedCheckpoints is exact through the read too.
			var reader = connection.GetReader(name, Handle);
			var queue = (IHandle<IMessage>)_queue;
			reader.PairedHandle = (message, checkpoint) => queue.Handle(new Delivered(message, checkpoint));
			return reader;
		};
		_getListener = () => connection.GetListener(name);
	}

	// The model whose queue is being drained on this thread, for as long as it is: what Emit checks.
	// A thread-static rather than the queue's thread id, since DirectApply dequeues on the caller's.
	[ThreadStatic] private static ReadModelBase? _dequeuing;

	/// <summary>
	/// Every message handled by the read model will pass through here.
	/// </summary>
	private void DequeueMessage(IMessage message) {
		var outer = _dequeuing;
		_dequeuing = this;
		try {
			Route(message);
		} finally {
			_dequeuing = outer;
		}
	}

	private void Route(IMessage message) {
		// Everything ahead of Relayed is this model's own bookkeeping, not events: not published, not counted.
		switch (message) {
			case ReadDrained drained:
				if (drained.Read is not null) {
					lock (ReaderLock) {
						Advance(drained.Read, OwnDelivery);
					}
				}
				RetireStream(drained.Generation);
				return;
			case TransitionRetry:
				RetryTransition();
				return;
			case CaptureBarrier barrier:
				RunCapture(barrier);
				return;
			case LiveCallback callback:
				RunLiveCallback(callback);
				return;
			case SourceDrained drained:
				lock (ReaderLock) {
					foreach (var checkpoint in drained.Applied)
						Advance(checkpoint, drained.Source);
				}
				RetireStream(drained.Generation);
				return;
			case SourceDetached detached:
				lock (ReaderLock) {
					Forget(detached.Source);
				}
				return;
			case Relayed relayed:
				DispatchRelayed(relayed);
				return;
			case Delivered delivered:
				Dispatch(delivered.Message, delivered.Checkpoint);
				return;
			default:
				Dispatch(message, null);
				return;
		}
	}

	private void Dispatch(IMessage message, StreamCheckpoint? checkpoint) {
		lock (ReaderLock) {
			// Before the handlers, so a handler asking where it stands is told the event it is applying.
			if (checkpoint is not null)
				Advance(checkpoint, OwnDelivery);
			DispatchToHandlers(message);
		}
	}

	private void DispatchRelayed(Relayed relayed) {
		lock (ReaderLock) {
			foreach (var checkpoint in relayed.Applied)
				Advance(checkpoint, relayed.Source);
			DispatchToHandlers(relayed.Change);
		}
	}

	/// <summary>Call under <see cref="ReaderLock"/>, with the checkpoints already advanced.</summary>
	private void DispatchToHandlers(IMessage message) {
		_bus.Handle(message);
		if (message is not StreamStoreMsgs.CatchupSubscriptionBecameLive)
			Version++;
		AfterDispatch();
	}

	// The source identity of everything that delivers a stream's own events — this model's readers
	// and listeners, and a CategoryStream relay — as against a model relay, which is its own.
	private static readonly object OwnDelivery = new();

	// Both written on the queue thread only, under ReaderLock, so a handler reads them consistently
	// with the state they sit beside. Per stream, what each source has delivered; and the reported
	// view, the least of those.
	private readonly Dictionary<string, Dictionary<object, StreamCheckpoint>> _appliedBySource = new(StringComparer.Ordinal);
	private readonly Dictionary<string, StreamCheckpoint> _applied = new(StringComparer.Ordinal);

	/// <summary>Moves a source's applied checkpoint for a stream forward, never back. Call under <see cref="ReaderLock"/>.</summary>
	/// <remarks>
	/// The stream's reported checkpoint is then re-read as the least across its sources: a model fed
	/// the same stream by two sources reflects it only as far as the slower one has said, and
	/// reporting further would name events its state lacks.
	/// </remarks>
	private void Advance(StreamCheckpoint checkpoint, object source) {
		if (!_appliedBySource.TryGetValue(checkpoint.StreamName, out var bySource)) {
			bySource = new Dictionary<object, StreamCheckpoint>(ReferenceEqualityComparer.Instance);
			_appliedBySource.Add(checkpoint.StreamName, bySource);
		}
		// A seed from a read arrives behind live events the listener queued before the sentinel, and
		// must not undo them; -1 orders "nothing yet" before version 0.
		if (bySource.TryGetValue(source, out var current) && VersionOrNone(current) > VersionOrNone(checkpoint))
			return;
		bySource[source] = checkpoint;
		_applied[checkpoint.StreamName] = Least(bySource.Values);
	}

	/// <summary>
	/// Drops a source's entries, so the streams it fed are reported from what remains. Call under
	/// <see cref="ReaderLock"/>.
	/// </summary>
	/// <remarks>
	/// A stream left with no source keeps its last reading: the state still reflects what that
	/// source delivered, and the stream is still named in a cut until something feeds it again.
	/// </remarks>
	private void Forget(object source) {
		foreach (var bySource in _appliedBySource) {
			if (bySource.Value.Remove(source) && bySource.Value.Count > 0)
				_applied[bySource.Key] = Least(bySource.Value.Values);
		}
	}

	private static StreamCheckpoint Least(IEnumerable<StreamCheckpoint> delivered) =>
		delivered.MinBy(VersionOrNone)!;

	private static long VersionOrNone(StreamCheckpoint checkpoint) => checkpoint.Version ?? -1;

	/// <summary>
	/// How far each stream has been <b>applied</b>: one checkpoint per stream, naming the last event
	/// this model's handlers have run — including the one being handled, if read from a handler.
	/// </summary>
	/// <remarks>
	/// <para>The applied counterpart of <see cref="GetCheckpoint"/>, which is delivered-not-applied.
	/// This one is safe to persist beside the state, because it names nothing the state lacks. It is
	/// exact only where it is read — on the queue thread: from a handler, an <see cref="OnceLive"/>
	/// callback, or <see cref="BufferedReadModelBase.Flush"/>. Read from anywhere else it is a
	/// snapshot the next dequeue may have moved past.</para>
	/// <para>Exact through the read phase and the live phase alike: the reader and the listener both
	/// hand each event over paired with that event's checkpoint. A stream that delivered nothing
	/// appears once its read drains, with a null version.</para>
	/// <para>A stream fed by a model relay (<see cref="RelayTo"/>) is named at the source's position
	/// when it emitted the last change this model applied. Where two sources fold the same stream, or
	/// a source folds one this model also reads, the entry is the <b>least</b> of their positions:
	/// this model's state reflects that stream only as far as the slowest of them has said, and any
	/// further reading would name events the state lacks. Per source the entry only ever moves
	/// forward; a source whose relay is disposed stops counting, and a stream left with no source
	/// keeps its last reading. Such an entry is not a position this model can resume the stream from
	/// — it does not read that stream; see <see cref="RelayTo"/>.</para>
	/// </remarks>
	protected IReadOnlyList<StreamCheckpoint> AppliedCheckpoints {
		get {
			lock (ReaderLock) {
				return _applied.Values.ToList();
			}
		}
	}

	private void RunLiveCallback(LiveCallback callback) {
		lock (_liveLock) {
			if (!_liveCallbacks.Remove(callback))
				return; // already abandoned
		}
		lock (ReaderLock) {
			callback.Run();
		}
	}

	private void AbandonLiveCallbacks() {
		List<LiveCallback> outstanding;
		lock (_liveLock) {
			outstanding = _liveCallbacks.ToList();
			_liveCallbacks.Clear();
		}
		foreach (var callback in outstanding) {
			Abandon(callback, null);
		}
	}

	/// <summary>
	/// Runs <paramref name="callback"/> once every started stream has drained — on the queue thread,
	/// under <see cref="ReaderLock"/>, before the next event is handled. Call it as many times as you
	/// need callbacks.
	/// </summary>
	/// <param name="callback">Runs once, sequenced with the handlers. Must not block or wait on this model.</param>
	/// <returns>
	/// Completes when the callback has run; faults with what it threw; cancelled if the model is
	/// disposed first or a start fails before the transition.
	/// </returns>
	/// <remarks>
	/// <para>The sequenced counterpart of <see cref="IsLive"/>. An <c>IsLive</c> continuation runs off
	/// the queue thread and races the next <c>Handle</c>; this runs at the transition itself, so a
	/// buffer accumulated during catch-up can be flushed with nothing arriving in between.</para>
	/// <para>Registered while the model is already live — nothing started, or everything drained — it
	/// is queued and runs behind whatever is queued now, still on the queue thread under the lock.
	/// That includes a model that has not started anything yet, so the same rule as <c>IsLive</c>
	/// holds: register after the last <c>Start</c>, or pass it to <see cref="StartAllAsync"/>.</para>
	/// <para>One registration, one run. A later <c>Start</c> that re-arms <c>IsLive</c> does not run it
	/// again; register another.</para>
	/// <para>Callbacks waiting on the same transition run in the order they were registered, each to
	/// completion before the next starts.</para>
	/// </remarks>
	public Task OnceLive(Action callback) {
		Ensure.NotNull(callback, nameof(callback));
		var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		LiveCallback registered;
		lock (_liveLock) {
			registered = new LiveCallback {
				Callback = callback,
				Completion = completion,
				Queued = _pendingStreams == 0 && _live.Task.IsCompletedSuccessfully
			};
			_liveCallbacks.Add(registered);
		}
		if (registered.Queued)
			Enqueue(registered);
		// A queue already stopped, or on its way there, will never dequeue it — nor reach the
		// transition it would otherwise wait for, once StopMessagePump has retired the streams.
		if (_closing)
			AbandonLiveCallbacks();
		return completion.Task;
	}

	/// <summary>
	/// Starts every stream in <paramref name="streams"/> and returns the task that completes once all
	/// of them have drained.
	/// </summary>
	/// <param name="streams">The streams to start. At least one, each on a distinct stream.</param>
	/// <param name="onceLive">
	/// Optionally, what to run at the transition — see <see cref="OnceLive"/>. Given one, the returned
	/// task is the callback's, which completes only after it has run.
	/// </param>
	/// <param name="cancelWaitToken">Cancels the reads this call starts.</param>
	/// <returns><see cref="IsLive"/>, covering every stream started here, or the callback's task.</returns>
	/// <exception cref="ArgumentException">Nothing to start, or two starters name one stream.</exception>
	/// <exception cref="InvalidOperationException">A stream is already started, or a capture is in flight.</exception>
	/// <remarks>
	/// <para>The whole set is resolved and checked before anything starts, so a rejected set leaves the
	/// model exactly as it was.</para>
	/// <para><see cref="IsLive"/> read between two <c>Start</c> calls covers only the first, and one
	/// read before any covers nothing. Reading it here, after every registration, is what makes the
	/// returned task cover all of them.</para>
	/// <para>Registering <paramref name="onceLive"/> through <see cref="OnceLive"/> after the last start
	/// would race the transition: every stream can drain in between, and the callback then runs behind
	/// whatever arrived rather than at the transition. Passing it here cannot — the call holds the model
	/// short of the transition until the callback is registered.</para>
	/// </remarks>
	public Task StartAllAsync(
		IReadOnlyList<StreamStarter> streams,
		Action? onceLive = null,
		CancellationToken cancelWaitToken = default) {
		Ensure.NotNull(streams, nameof(streams));
		if (streams.Count == 0) {
			throw new ArgumentException(
				$"{GetType().Name}.{nameof(StartAllAsync)} was given no streams, so the task it would " +
				"return describes nothing. Pass at least one.", nameof(streams));
		}

		var names = new string[streams.Count];
		var seen = new HashSet<string>(StringComparer.Ordinal);
		for (var i = 0; i < streams.Count; i++) {
			if (streams[i] is null)
				throw new ArgumentException("A stream starter is null.", nameof(streams));
			names[i] = streams[i].StreamName(_namer);
			if (!seen.Add(names[i])) {
				throw new ArgumentException(
					$"'{names[i]}' appears twice. A model reads a stream once.", nameof(streams));
			}
			EnsureStreamNotStarted(names[i]);
		}

		// Held across the whole sequence so the model cannot reach the transition part-way through it:
		// the callback registers as pending rather than queued, and the task read at the end covers
		// every stream. Released through the queue, behind everything the reads delivered.
		var gate = RegisterStream();
		try {
			for (var i = 0; i < streams.Count; i++)
				StartAsync(names[i], streams[i].Checkpoint, streams[i].ValidateStream, cancelWaitToken);
			var live = onceLive is null ? IsLive : OnceLive(onceLive);
			MarkReadDrained(gate, null);
			return live;
		} catch {
			RetireAllStreams(null);
			throw;
		}
	}

	private readonly List<ModelRelay> _relays = [];

	/// <summary>
	/// Makes this model a source for <paramref name="target"/>: every change this model
	/// <see cref="Emit"/>s is handed to the target's queue paired with this model's applied
	/// checkpoints, for as long as the returned subscription is held.
	/// </summary>
	/// <param name="target">The read model that derives from this one's state.</param>
	/// <returns>A subscription; disposing it detaches the relay.</returns>
	/// <exception cref="ArgumentException"><paramref name="target"/> is this model.</exception>
	/// <exception cref="InvalidOperationException">
	/// The target is being captured, or this model has folded something and the call is off its queue thread.
	/// </exception>
	/// <exception cref="ObjectDisposedException">This model or <paramref name="target"/> has been disposed.</exception>
	/// <remarks>
	/// <para><b>When to use this rather than <see cref="CategoryStream{TAggregate}.RelayTo"/>.</b> That
	/// relays a category's <i>events</i>, and the target folds them itself. This relays what this model
	/// makes of its events: a change it raises from a handler, carrying the checkpoints of exactly what
	/// it had applied when it raised it. Choose it where the target derives from this model's state —
	/// an enrichment, a join resolved here — so the fold happens once, here, and not again in every
	/// dependent model.</para>
	/// <para><b>Attach before anything is folded, or from this model's queue thread.</b> A change
	/// reaches only the relays attached when it is raised. A relay attached while this model's
	/// <see cref="Version"/> is still zero — before its first <c>Start</c>, in practice — is handed
	/// everything this model folds. Once anything has been folded, attach only from a handler or an
	/// <see cref="OnceLive"/> callback — where <see cref="Emit"/> is allowed — and <see cref="Emit"/>
	/// what the target lacks, a snapshot of this model's state, before returning: the release hands the
	/// target this model's whole position, and a target attached late reflects that position only if
	/// it was handed the state the position names. Nothing can be folded between the attach and that
	/// emit. Off the queue thread a late attach is refused.</para>
	/// <para><b>Liveness.</b> The target counts this model as a source until this model's live
	/// transition, so the target's <see cref="IsLive"/> and <see cref="OnceLive"/> wait for this
	/// model's history and for every change raised while folding it — the same as for a stream the
	/// target read itself. Anything that ends this source without a transition — disposing the
	/// subscription, disposing this model, a start that fails — releases the target rather than
	/// faulting it, and the failure is reported on this model's <see cref="IsLive"/>.</para>
	/// <para><b>Checkpoints.</b> Each change advances the target's <see cref="AppliedCheckpoints"/>
	/// for the streams this model folds, and the release at the transition seeds them for a source
	/// that raised nothing.</para>
	/// <para><b>Not a resume point.</b> The target does not read the streams this relay names, and
	/// this relay takes no position, so the target cannot resume them from a checkpoint it persisted.
	/// A target restarted is rebuilt through its source: attach before the source's first start, or
	/// attach late with a snapshot as above. What its checkpoints are good for is
	/// <see cref="StreamCheckpoint.BoundedBy(StreamCheckpoint?)"/> — bounding another model's resume
	/// by where this target's state stood.</para>
	/// <para><b>Nothing runs across the two models.</b> The forward happens on this model's queue
	/// thread and only enqueues on the target's, so no lock is held on both at once.</para>
	/// <para>A target disposed while attached detaches this relay the next time this model emits; the
	/// subscription may still be disposed.</para>
	/// <para>A relay that would close a cycle — the target already relays to this model, directly or
	/// through others — is refused: every model in the cycle would wait for a transition another
	/// holds open, and none would go live. Checked against the relays attached when this is called.</para>
	/// </remarks>
	public IDisposable RelayTo(ReadModelBase target) {
		Ensure.NotNull(target, nameof(target));
		if (Reaches(target, this)) {
			throw new ArgumentException(ReferenceEquals(target, this)
				? $"{GetType().Name} cannot relay to itself: the registration it would make is released at " +
				  "its own live transition, which that registration holds open."
				: $"{GetType().Name} cannot relay to {target.GetType().Name}, which already relays to it: each " +
				  "would hold the other's live transition open, and neither would go live.", nameof(target));
		}
		ObjectDisposedException.ThrowIf(_closing, this);
		ModelRelay relay;
		// Under the lock the handlers fold under, so nothing can be folded between the check and the
		// attach: a relay attached here is handed every change from the next dispatch on.
		lock (ReaderLock) {
			if (Version != 0 && !ReferenceEquals(_dequeuing, this)) {
				throw new InvalidOperationException(
					$"{GetType().Name} has already folded something, so a relay attached here would be released " +
					"at this model's whole position over a target holding none of it. Attach every relay before " +
					$"the first start, or attach from a handler or an {nameof(OnceLive)} callback and " +
					$"{nameof(Emit)} the state the target lacks before returning.");
			}
			// The target does not read this model's streams, so nothing else would hold its IsLive
			// open until this model has handed over what it raised while folding its history. Its own
			// registration refuses a closed target, whose stopped queue would never dequeue the release.
			relay = new ModelRelay(this, target, target.RegisterExternalSource());
			lock (_relays) {
				_relays.Add(relay);
			}
		}
		bool closing, awaitStart;
		lock (_liveLock) {
			// Decided under the lock against the flag StopMessagePump sets before it drains the
			// awaiting list, so a relay added here is either drained there or refused here.
			closing = _closing;
			// A model that has started nothing is vacuously live, and a release armed now would be
			// queued at once — over a target holding none of what this model is about to fold. It
			// waits for the first start instead; nothing this model raises can precede that.
			awaitStart = !closing && _registrations == 0;
			if (awaitStart)
				_relaysAwaitingStart.Add(relay);
		}
		if (closing) {
			relay.Dispose();
			throw new ObjectDisposedException(GetType().Name);
		}
		if (!awaitStart)
			ArmRelease(relay);
		return relay;
	}

	// Relays attached before anything was started, to be armed by the first registration. Guarded by
	// _liveLock, with _registrations: whether a relay waits here is decided against that count.
	private readonly List<ModelRelay> _relaysAwaitingStart = [];

	/// <summary>Whether a chain of relays leads from <paramref name="from"/> to <paramref name="to"/>.</summary>
	private static bool Reaches(ReadModelBase from, ReadModelBase to) {
		var seen = new HashSet<ReadModelBase>(ReferenceEqualityComparer.Instance);
		var pending = new Stack<ReadModelBase>();
		pending.Push(from);
		while (pending.Count > 0) {
			var model = pending.Pop();
			if (ReferenceEquals(model, to))
				return true;
			if (!seen.Add(model))
				continue;
			ModelRelay[] relays;
			lock (model._relays) {
				relays = model._relays.ToArray();
			}
			foreach (var relay in relays)
				pending.Push(relay.Target);
		}
		return false;
	}

	/// <summary>Releases the relay's target at this model's transition, behind every change forwarded before it.</summary>
	/// <remarks>
	/// Runs on this queue thread under <see cref="ReaderLock"/>, so the checkpoints it carries are
	/// exact. Armed while already live, the release goes on the queue behind what is there.
	/// </remarks>
	private void ArmRelease(ModelRelay relay) {
		_ = OnceLive(() => relay.Drain(AppliedCheckpoints)).ContinueWith(
			t => {
				// Cancelled or faulted: this model will not make the transition the release was
				// waiting for, and the target must not wait for it either.
				relay.Drain();
				_ = t.Exception;
			},
			CancellationToken.None,
			TaskContinuationOptions.NotOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously,
			TaskScheduler.Default);
	}

	/// <summary>Releases the targets of relays still waiting for a first start that will not come.</summary>
	private void ReleaseRelaysAwaitingStart() {
		ModelRelay[] awaiting;
		lock (_liveLock) {
			awaiting = _relaysAwaitingStart.ToArray();
			_relaysAwaitingStart.Clear();
		}
		foreach (var relay in awaiting)
			relay.Drain();
	}

	/// <summary>
	/// One <see cref="RelayTo"/> subscription: forwards onto the target's queue, and releases the
	/// target's registration once.
	/// </summary>
	private sealed class ModelRelay(ReadModelBase source, ReadModelBase target, int generation) : IDisposable {
		private int _disposed;
		private int _drained;
		// What this relay last handed over: the honest position for a release made off the source's
		// queue thread, where the source's own checkpoints are already a snapshot.
		private volatile IReadOnlyList<StreamCheckpoint> _forwarded = [];

		public ReadModelBase Target => target;

		/// <summary>Enqueues <paramref name="change"/> on the target with the source's reading at the emit.</summary>
		/// <remarks>A target that has closed has stopped its queue; forwarding into it would only grow it, so the relay detaches instead.</remarks>
		public void Forward(IMessage change, IReadOnlyList<StreamCheckpoint> applied) {
			if (Volatile.Read(ref _disposed) != 0)
				return;
			if (target._closing) {
				Dispose();
				return;
			}
			_forwarded = applied;
			target.Enqueue(new Relayed(change, applied, this));
		}

		/// <summary>Releases the target's registration for this source, once.</summary>
		/// <remarks>
		/// Carries <paramref name="applied"/>, or what was last forwarded when the caller has no exact
		/// reading. Interlocked because the transition releases from the source's queue thread while a
		/// consumer may be disposing the same relay. A closed target has retired everything outstanding
		/// and dequeues nothing, so it is not told.
		/// </remarks>
		public void Drain(IReadOnlyList<StreamCheckpoint>? applied = null) {
			if (Interlocked.Exchange(ref _drained, 1) != 0)
				return;
			if (target._closing)
				return;
			target.Enqueue(new SourceDrained(generation, applied ?? _forwarded, this));
		}

		/// <summary>Detaches from the source, releases the target, and stops bounding the target's streams.</summary>
		/// <remarks>
		/// Detaching before the transition still releases: the target is no longer fed this source, so
		/// leaving its registration open would hang anyone awaiting its IsLive with nothing to arrive.
		/// </remarks>
		public void Dispose() {
			if (Interlocked.Exchange(ref _disposed, 1) != 0)
				return;
			source.Detach(this);
			Drain();
			if (!target._closing)
				target.Enqueue(new SourceDetached(this));
		}
	}

	/// <summary>
	/// Hands <paramref name="change"/> to every model this one relays to, paired with this model's
	/// <see cref="AppliedCheckpoints"/> as they stand now.
	/// </summary>
	/// <param name="change">
	/// What changed, in this model's terms — not the event being folded. The target is told what this
	/// model made of the event, which is what <see cref="RelayTo"/> is for.
	/// </param>
	/// <exception cref="InvalidOperationException">Called off this model's queue thread.</exception>
	/// <remarks>
	/// <para>Call it from a handler, an <see cref="OnceLive"/> callback or a
	/// <see cref="BufferedReadModelBase.Flush"/>: the places where <see cref="AppliedCheckpoints"/> is
	/// exact. Anywhere else the checkpoints are a snapshot the next dequeue may have moved past, and a
	/// change paired with them would overstate what its targets reflect — so this throws rather than
	/// pair them.</para>
	/// <para>Enqueues on each target and returns; no target's handler runs here. With no relay
	/// attached it does nothing.</para>
	/// </remarks>
	protected void Emit(IMessage change) {
		Ensure.NotNull(change, nameof(change));
		if (!ReferenceEquals(_dequeuing, this)) {
			throw new InvalidOperationException(
				$"{GetType().Name}.{nameof(Emit)} must be called on the model's queue thread — from a handler, " +
				$"an {nameof(OnceLive)} callback or a flush. {nameof(AppliedCheckpoints)} is exact only there, and " +
				"a change paired with a stale checkpoint would overstate what its targets reflect.");
		}
		ModelRelay[] relays;
		lock (_relays) {
			relays = _relays.ToArray();
		}
		if (relays.Length == 0)
			return;
		// One reading for every target: what this model has applied at this point, exactly.
		var applied = AppliedCheckpoints;
		foreach (var relay in relays)
			relay.Forward(change, applied);
	}

	private void Detach(ModelRelay relay) {
		lock (_relays) {
			_relays.Remove(relay);
		}
		lock (_liveLock) {
			_relaysAwaitingStart.Remove(relay);
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
	/// state with no checkpoint naming them. A source attached with <see cref="RelayTo"/> that has not
	/// yet released this model counts as still reading.</para>
	/// <para>A stream a model relay feeds is named in the cut from <see cref="AppliedCheckpoints"/>,
	/// which is exact where the barrier is reached, and at the least-of-sources reading described
	/// there. A model with no streams of its own therefore captures its sources' streams. A stream a
	/// <see cref="CategoryStream{TAggregate}"/> relays is not named: that read is owned elsewhere, and
	/// its position is the stream's <see cref="CategoryStream{TAggregate}.PositionAtGoLive"/>.</para>
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
			Enqueue(barrier);
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
				barrier.Complete(WithRelayedStreams(barrier.Checkpoints));
			}
		} catch (Exception ex) {
			barrier.Abandon(ex);
		}
	}

	/// <summary>
	/// The cut's checkpoints: <paramref name="sampled"/> from the listeners, with every stream a model
	/// relay feeds named from <see cref="AppliedCheckpoints"/> instead. Call under <see cref="ReaderLock"/> at the barrier.
	/// </summary>
	/// <remarks>
	/// Exact there for relay-fed streams: relayed changes reach this queue paired and in order, so what
	/// has been applied of them when the barrier is dequeued is precisely what the cut covers. A stream
	/// only this model's own delivery feeds is left as sampled.
	/// </remarks>
	private IReadOnlyList<StreamCheckpoint> WithRelayedStreams(IReadOnlyList<StreamCheckpoint> sampled) {
		List<StreamCheckpoint>? cut = null;
		foreach (var (stream, bySource) in _appliedBySource) {
			if (bySource.Count == 1 && bySource.ContainsKey(OwnDelivery))
				continue;
			cut ??= sampled.ToList();
			cut.RemoveAll(c => string.Equals(c.StreamName, stream, StringComparison.Ordinal));
			cut.Add(_applied[stream]);
		}
		return cut ?? sampled;
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

	/// <summary>
	/// Refuses a stream this model is already listening to.
	/// </summary>
	/// <remarks>
	/// <para>Two listeners on one stream both feed this model's single queue, and the model has one set
	/// of handlers, so every event on that stream is handled twice with nothing able to tell the two
	/// deliveries apart. A handler that accumulates rather than recomputes is wrong from the second
	/// delivery on, and <see cref="GetCheckpoint"/> reports the stream twice.</para>
	/// <para>Disposal is the release: a disposed listener no longer delivers, so a stream can be
	/// started again after its listener drops.</para>
	/// <para>This does not make delivery unique. A category stream and a member aggregate's own stream
	/// are different names carrying overlapping events, and so are two categories whose membership
	/// overlaps; either pair delivers an event twice and neither is refused here.</para>
	/// </remarks>
	/// <exception cref="InvalidOperationException">A live listener already reads this stream.</exception>
	private void EnsureStreamNotStarted(string stream) {
		lock (_listeners) {
			if (!_listeners.Any(l => !l.IsDisposed && string.Equals(l.StreamName, stream, StringComparison.Ordinal)))
				return;
		}
		throw new InvalidOperationException(
			$"{GetType().Name} is already listening to '{stream}'. A second listener would hand this " +
			"model every event on that stream twice.");
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
		// Paired delivery rather than EventStream, so each event reaches the queue with its own
		// checkpoint — what AppliedCheckpoints is built from.
		var queue = (IHandle<IMessage>)_queue;
		l.SubscribeToDelivery((message, checkpoint) => queue.Handle(new Delivered(message, checkpoint)));
		_ = l.SubscriptionLost.ContinueWith(
			t => {
				if (t.IsFaulted)
					_subscriptionsLost.TrySetException(t.Exception!.InnerExceptions);
			},
			CancellationToken.None,
			TaskContinuationOptions.ExecuteSynchronously,
			TaskScheduler.Default);
		return l;
	}

	/// <summary>
	/// Attaches a listener where a read left off and reports where that is, for the sentinel to carry.
	/// </summary>
	/// <param name="reader">The reader that has just replayed the stream's history.</param>
	/// <param name="resumeFrom">The checkpoint the start was asked to resume after, if any.</param>
	/// <param name="start">Starts the listener from the version handed to it.</param>
	private StreamCheckpoint? Attach(IStreamReader reader, long? resumeFrom, Action<IListener, long?> start) {
		// One read of the reader: version and position must come from the same event. A read that
		// delivered nothing leaves the stream where the caller said it was, not at its beginning.
		var read = reader.Checkpoint;
		var position = read?.Version ?? resumeFrom;
		var listener = AddNewListener(read?.Position);
		start(listener, position);
		// The listener's name, not the reader's: a reader that found no stream never names one. A
		// listener that names nothing either cannot be seeded, and its first event names it instead.
		return read ?? (string.IsNullOrEmpty(listener.StreamName) ? null : new StreamCheckpoint(listener.StreamName, position));
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
	/// quiet, or once <see cref="Idle"/> holds and stays held. From the queue thread, read
	/// <see cref="AppliedCheckpoints"/> instead: it names exactly what the handlers have run.</para>
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
		EnsureStreamNotStarted(stream);
		RunStart(() => {
			using var reader = _getReader();
			reader.Read(stream, () => ReadCompleted, checkpoint);
			if (_disposed)
				return null;
			return Attach(reader, checkpoint,
				(l, position) => l.Start(stream, position, blockUntilLive, validateStream, cancelWaitToken));
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
		EnsureStreamNotStarted(stream);
		RunStartAsync(() => {
			using var reader = _getReader();
			reader.Read(stream, () => ReadCompleted, checkpoint);
			if (_disposed)
				return null;
			return Attach(reader, checkpoint,
				(l, position) => l.Start(stream, position, false, validateStream, cancelWaitToken));
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
		EnsureStreamNotStarted(_namer.GenerateForAggregate(typeof(TAggregate), id));
		RunStart(() => {
			using var reader = _getReader();
			reader.Read<TAggregate>(id, () => ReadCompleted, checkpoint);
			if (_disposed)
				return null;
			return Attach(reader, checkpoint,
				(l, position) => l.Start<TAggregate>(id, position, blockUntilLive, validateStream, cancelWaitToken));
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
		EnsureStreamNotStarted(_namer.GenerateForAggregate(typeof(TAggregate), id));
		RunStartAsync(() => {
			using var reader = _getReader();
			reader.Read<TAggregate>(id, () => ReadCompleted, checkpoint);
			if (_disposed)
				return null;
			return Attach(reader, checkpoint,
				(l, position) => l.Start<TAggregate>(id, position, false, validateStream, cancelWaitToken));
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
		EnsureStreamNotStarted(_namer.GenerateForCategory(typeof(TAggregate)));
		RunStart(() => {
			using var reader = _getReader();
			reader.Read<TAggregate>(() => ReadCompleted, checkpoint);
			if (_disposed)
				return null;
			return Attach(reader, checkpoint,
				(l, position) => l.Start<TAggregate>(position, blockUntilLive, validateStream, cancelWaitToken));
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
		EnsureStreamNotStarted(_namer.GenerateForCategory(typeof(TAggregate)));
		RunStartAsync(() => {
			using var reader = _getReader();
			reader.Read<TAggregate>(() => ReadCompleted, checkpoint);
			if (_disposed)
				return null;
			return Attach(reader, checkpoint,
				(l, position) => l.Start<TAggregate>(position, false, validateStream, cancelWaitToken));
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
		// Listeners first, so a snapshot taken next cannot see an event that arrives after the cut.
		BeforeStopping();
		_queue.Stop();
		// The queue is stopped, so a sentinel still in it will never be dequeued; release anyone
		// awaiting IsLive rather than leave them on a stream that can no longer drain.
		RetireAllStreams(null);
		// Same for a capture waiting on a marker that can no longer arrive. After the listeners are
		// disposed, so a hold this releases cannot be retaken.
		AbandonCaptures(null);
		AbandonLiveCallbacks();
		ReleaseRelaysAwaitingStart();
		_subscriptionsLost.TrySetCanceled();
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

	/// <summary>
	/// Enqueues <paramref name="message"/> for the handlers — the way to inject a message from outside
	/// the model's own listeners and still fold it on the queue.
	/// </summary>
	/// <remarks>
	/// <see cref="DirectApply"/> is the other way in, applying on the calling thread rather than the
	/// queue; <c>ReactiveDomain.Testing</c>'s <c>UpdateFromAggregate</c> is built on it. Prefer this
	/// one outside tests.
	/// </remarks>
	/// <remarks>
	/// Do not add a public <c>Handle(T)</c> for a type the model implements <c>IHandle&lt;T&gt;</c>
	/// for: a caller writing <c>model.Handle(evt)</c> then binds to that method, runs the handler on
	/// the calling thread, and skips this queue. Implement the interface explicitly, and use
	/// <see cref="Publish"/> or this method to inject.
	/// </remarks>
	public void Handle(Message message) => Enqueue(message);

	/// <inheritdoc cref="Handle(Message)"/>
	public void Handle(IMessage message) => Enqueue(message);

	/// <summary>Enqueues <paramref name="message"/> paired with that message's checkpoint.</summary>
	internal void Handle(IMessage message, StreamCheckpoint? checkpoint) =>
		Enqueue(new Delivered(message, checkpoint));

	private void Enqueue(IMessage message) => ((IHandle<IMessage>)_queue).Handle(message);

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
