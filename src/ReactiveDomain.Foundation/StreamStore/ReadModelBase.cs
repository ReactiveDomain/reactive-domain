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
	/// the same as the version of any particular stream being read. This can
	/// include <see cref="StreamStoreMsgs.CatchupSubscriptionBecameLive"/>,
	/// which may result in the Version being 1 greater than otherwise expected.
	/// </summary>
	public int Version { get; private set; }

	private readonly object _liveLock = new();
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
	/// <para>The task faults if a start path throws before its listener is attached. It also faults if
	/// the model's own transition throws — a <see cref="BufferedReadModelBase"/> whose first flush
	/// fails.</para>
	/// <para>It is cancelled if the model is disposed with streams still outstanding, so an awaiting
	/// caller is never left on a stream that can no longer drain.</para>
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
			_registrations++;
			return _generation;
		}
	}

	/// <summary>
	/// Retires one outstanding stream. When the last one drains, runs the live transition on this
	/// thread — the queue's — and then completes the armed task.
	/// </summary>
	private void RetireStream(int generation) {
		TaskCompletionSource? drained = null;
		List<LiveCallback>? callbacks = null;
		lock (_liveLock) {
			// A sentinel outlives the streams it was queued alongside when one of them fails, and the
			// count it would decrement by then belongs to whatever started next. Stamping it keeps it
			// from retiring a stream it never described.
			if (generation != _generation || _pendingStreams == 0)
				return;
			if (--_pendingStreams == 0) {
				drained = _live;
				callbacks = TakeWaitingCallbacks();
			}
		}
		if (drained is null)
			return;
		// Under ReaderLock and ahead of the next dequeue, so the transition is sequenced with the
		// handlers exactly as an event is. A failure here faults the armed task rather than reporting
		// live over a model whose own transition did not complete.
		Exception? failure = null;
		lock (ReaderLock) {
			try {
				AtLiveTransition();
			} catch (Exception ex) {
				failure = ex;
			}
			foreach (var callback in callbacks!) {
				callback.Run();
			}
		}
		// Signalled outside the lock: an awaiter released here may take _liveLock.
		if (failure is null)
			drained.TrySetResult();
		else
			drained.TrySetException(failure);
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
	/// Runs on the queue thread, under <see cref="ReaderLock"/>, after each message has been through
	/// the handlers.
	/// </summary>
	internal virtual void AfterDispatch() { }

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
			if (_pendingStreams == 0)
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
	/// Where the read left the stream, so <see cref="AppliedCheckpoints"/> can take it up at the point
	/// the read's events have all been applied. The read delivers bare messages, so nothing else
	/// carries that position.
	/// </param>
	private void MarkReadDrained(int generation, StreamCheckpoint? read) =>
		((IHandle<IMessage>)_queue).Handle(new ReadDrained(generation, read));

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
	internal void MarkExternalSourceDrained(int generation) => MarkReadDrained(generation, null);

	private sealed record ReadDrained(int Generation, StreamCheckpoint? Read) : IMessage {
		public Guid MsgId { get; } = Guid.NewGuid();
	}

	/// <summary>An event off a listener, with the checkpoint that names it — see <see cref="IListener.SubscribeToDelivery"/>.</summary>
	private sealed record Delivered(IMessage Message, StreamCheckpoint? Checkpoint) : IMessage {
		public Guid MsgId => Message.MsgId;
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
		// The first four are this model's own bookkeeping, not events: not published, not counted.
		switch (message) {
			case ReadDrained drained:
				if (drained.Read is not null) {
					lock (ReaderLock) {
						Advance(drained.Read);
					}
				}
				RetireStream(drained.Generation);
				return;
			case CaptureBarrier barrier:
				RunCapture(barrier);
				return;
			case LiveCallback callback:
				RunLiveCallback(callback);
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
				Advance(checkpoint);
			_bus.Handle(message);
			Version++;
			AfterDispatch();
		}
	}

	// Written on the queue thread only, under ReaderLock, so a handler reads it consistently with
	// the state it sits beside.
	private readonly Dictionary<string, StreamCheckpoint> _applied = new(StringComparer.Ordinal);

	/// <summary>Moves a stream's applied checkpoint forward, never back. Call under <see cref="ReaderLock"/>.</summary>
	private void Advance(StreamCheckpoint checkpoint) {
		// A seed from a read arrives behind live events the listener queued before the sentinel, and
		// must not undo them; -1 orders "nothing yet" before version 0.
		if (_applied.TryGetValue(checkpoint.StreamName, out var current) && (current.Version ?? -1) > (checkpoint.Version ?? -1))
			return;
		_applied[checkpoint.StreamName] = checkpoint;
	}

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
	/// <para>A stream still in its read phase is reported where it stood before the read: the read
	/// delivers bare messages, and its entry catches up when the read drains. From that point, and for
	/// every event the listener delivers after it, the entry is exact.</para>
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
			registered = new LiveCallback { Callback = callback, Completion = completion, Queued = _pendingStreams == 0 };
			_liveCallbacks.Add(registered);
		}
		if (registered.Queued) {
			((IHandle<IMessage>)_queue).Handle(registered);
			// A queue already stopped, or on its way there, will never dequeue it.
			if (_closing)
				AbandonLiveCallbacks();
		}
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
		// Paired delivery rather than EventStream, so each event reaches the queue with the checkpoint
		// that names it — what AppliedCheckpoints is built from.
		var queue = (IHandle<IMessage>)_queue;
		l.SubscribeToDelivery((message, checkpoint) => queue.Handle(new Delivered(message, checkpoint)));
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

		_queue.Stop();
		// The queue is stopped, so a sentinel still in it will never be dequeued; release anyone
		// awaiting IsLive rather than leave them on a stream that can no longer drain.
		RetireAllStreams(null);
		// Same for a capture waiting on a marker that can no longer arrive. After the listeners are
		// disposed, so a hold this releases cannot be retaken.
		AbandonCaptures(null);
		AbandonLiveCallbacks();
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
