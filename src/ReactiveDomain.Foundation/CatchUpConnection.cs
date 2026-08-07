using ReactiveDomain.Messaging;

namespace ReactiveDomain.Foundation;

/// <summary>
/// An <see cref="IConfiguredConnection"/> decorator providing a deterministic "all read models
/// have consumed everything committed to their streams" barrier. Listeners handed out by
/// <see cref="GetListener"/> are tracked; <see cref="WaitForCatchUp"/> blocks until every tracked
/// listener has delivered through its stream's current end and every supplied read model is idle
/// with an empty queue. Replaces heuristic waits (count-stability windows, version guessing) whose
/// failure mode is false completion under scheduler lag. Useful in production seeding and export
/// paths as well as tests.
/// </summary>
/// <remarks>
/// This extends <see cref="ReadModelBase.IsLive"/> rather than compensating for it.
/// <c>IsLive</c> is the hydration barrier — it completes once every started stream's history has
/// been handled — but it is bounded by each stream's read phase. Events committed <i>after</i> that,
/// which is the case seeding and export paths care about, are outside it; those are what the tracked
/// listener positions and the read-model queue check below cover.
/// </remarks>
public sealed class CatchUpConnection(IConfiguredConnection inner) : IConfiguredConnection {
	private readonly List<IListener> _tracked = [];

	public IStreamStoreConnection Connection => inner.Connection;
	public IStreamNameBuilder StreamNamer => inner.StreamNamer;
	public IEventSerializer Serializer => inner.Serializer;

	/// <summary>
	/// Returns a listener whose delivered-through position feeds <see cref="WaitForCatchUp"/>.
	/// </summary>
	public IListener GetListener(string name) {
		// Constructed rather than delegated: what this barrier promises rests on the listener being
		// this one, not on whatever the wrapped connection hands out.
		var listener = new StreamListener(name, inner.Connection, inner.StreamNamer, inner.Serializer);
		lock (_tracked) { _tracked.Add(listener); }
		return listener;
	}

	/// <summary>
	/// The wrapped connection's queued listener, which this barrier does not track.
	/// </summary>
	/// <remarks>
	/// <see cref="WaitForCatchUp"/> spans the listeners handed out by <see cref="GetListener"/>. A
	/// model fed from a queued listener is outside it and needs a barrier of its own.
	/// </remarks>
	public IListener GetQueuedListener(string name) => inner.GetQueuedListener(name);

	public IStreamReader GetReader(string name, Action<IMessage> handle) => inner.GetReader(name, handle);

	public IRepository GetRepository(bool caching = false, Func<Guid>? currentPolicyUserId = null) =>
		inner.GetRepository(caching, currentPolicyUserId);

	public ICorrelatedRepository GetCorrelatedRepository(
		IRepository? baseRepository = null, bool caching = false, Func<Guid>? currentPolicyUserId = null) =>
		inner.GetCorrelatedRepository(baseRepository, caching, currentPolicyUserId);

	/// <summary>
	/// Blocks until every listener handed out by <see cref="GetListener"/> has delivered through
	/// its stream's current end and every read model in <paramref name="readModels"/> is idle
	/// with an empty queue. The stream-end target is re-read every pass, so it keeps moving until
	/// the store is quiet. Throws a <see cref="TimeoutException"/> naming each lagging stream and
	/// each busy read-model queue.
	/// </summary>
	/// <param name="timeout">Overall deadline for the barrier, including the IsLive wait.</param>
	/// <param name="readModels">The read models whose queues must drain.</param>
	public void WaitForCatchUp(TimeSpan timeout, params ReadModelBase[] readModels) {
		var deadline = DateTime.UtcNow + timeout;

		// Hydration is a single wait, not a poll: ReadModelBase.IsLive completes only once every
		// started stream has dispatched its history through the model's handlers. Bounded — an
		// unbounded wait here has hung CI for hours.
		var isLive = Task.WhenAll(readModels.Select(rm => rm.IsLive).ToArray());
		try {
			if (!isLive.Wait(timeout)) {
				throw new TimeoutException($"Read models did not go live within {timeout}.");
			}
		} catch (AggregateException ex) {
			// IsLive faults when a start path threw and cancels when the model was disposed with
			// streams outstanding. Neither can complete later, so do not fall through to polling.
			throw new TimeoutException("A read model faulted or was disposed before going live.", ex);
		}

		// Queryability includes the go-live cache flushes that ride ModelWentLive. An armed model's
		// marker is a task-pool continuation off IsLive and can lose the race to the idle check
		// below, so enqueue one here — behind the histories the wait just proved delivered, and
		// idempotent by the marker's contract when the armed one also lands.
		foreach (var rm in readModels) { rm.Handle(new ModelWentLive()); }

		// What remains is the residue IsLive does not span: events committed after the read phase,
		// and listeners started outside any read model. Those have no in-band completion signal, so
		// this part stays a poll.
		while (true) {
			var laggards = ListLaggards(readModels);
			if (laggards.Count == 0) { return; }
			if (DateTime.UtcNow > deadline) {
				throw new TimeoutException($"Catch-up incomplete after {timeout}: {string.Join("; ", laggards)}");
			}
			Thread.Sleep(10);
		}
	}

	// Checked in causal order each pass: store delivery first, then read-model queues.
	private List<string> ListLaggards(ReadModelBase[] readModels) {
		var laggards = new List<string>();
		IListener[] tracked;
		lock (_tracked) {
			// A disposed listener can never deliver again; keeping it would pin the barrier below a
			// moving stream tail whenever a model is deliberately torn down while the connection
			// lives on — snapshot-restore and kill/resume flows do exactly that. A subscription that
			// merely drops leaves its listener undisposed, so that failure still reads as a laggard.
			_tracked.RemoveAll(static listener => listener.IsDisposed);
			tracked = _tracked.ToArray();
		}
		foreach (var listener in tracked) {
			// A checkpoint arrives with the stream name, so an unnamed listener has not started.
			if (listener.Checkpoint is not { } checkpoint) { continue; }
			var slice = inner.Connection.ReadStreamBackward(checkpoint.StreamName, -1, 1);
			if (slice is null or StreamNotFoundSlice || slice.Events.Length == 0) { continue; }
			var target = slice.LastEventNumber;
			// Recorded once the event is in the subscriber's queue, and null until there is one, so
			// this is "in the read model's queue or applied" — never "in flight", never a seed 0
			// passing for event 0. -1 is before every event, which is what no version means here.
			var delivered = checkpoint.Version ?? -1;
			if (delivered < target) {
				laggards.Add($"{checkpoint.StreamName} delivered {delivered} of {target}");
			}
		}
		if (laggards.Count > 0) { return laggards; }
		foreach (var rm in readModels) {
			if (!rm.Idle || rm.MessageCount != 0) {
				laggards.Add($"queue {rm.GetType().Name} (count {rm.MessageCount})");
			}
		}
		return laggards;
	}

}
