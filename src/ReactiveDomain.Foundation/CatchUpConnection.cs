using ReactiveDomain.Messaging;

namespace ReactiveDomain.Foundation;

/// <summary>
/// An <see cref="IConfiguredConnection"/> decorator providing a deterministic "all read models
/// have consumed everything committed to their streams" barrier. Listeners handed out by
/// <see cref="GetListener"/> are tracked; <see cref="WaitForCatchUp"/> blocks until every tracked
/// listener has delivered through its stream's current end and every supplied read model is idle
/// with an empty queue. Replaces heuristic waits (count-stability windows, version guessing, bare
/// IsLive) whose failure mode is false completion under scheduler lag. Useful in production
/// seeding and export paths as well as tests.
/// </summary>
public sealed class CatchUpConnection(IConfiguredConnection inner) : IConfiguredConnection {
	private readonly List<TrackedStreamListener> _tracked = [];

	public IStreamStoreConnection Connection => inner.Connection;
	public IStreamNameBuilder StreamNamer => inner.StreamNamer;
	public IEventSerializer Serializer => inner.Serializer;

	/// <summary>
	/// Returns a listener whose delivered-through position feeds <see cref="WaitForCatchUp"/>.
	/// </summary>
	public IListener GetListener(string name) {
		var listener = new TrackedStreamListener(name, inner.Connection, inner.StreamNamer, inner.Serializer);
		lock (_tracked) { _tracked.Add(listener); }
		return listener;
	}

	/// <summary>
	/// Deliberately NOT tracked: <see cref="QueuedStreamListener"/>'s internal queue
	/// buffers events after receipt, so a tracked position would over-report delivery —
	/// "received by the listener" is not "handed to the subscriber". Do not "fix" this by
	/// tracking it.
	/// </summary>
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

		// Bound the IsLive wait: an unbounded wait here has hung CI for hours.
		var isLive = Task.WhenAll(readModels.Select(rm => rm.IsLive).ToArray());
		try {
			if (!isLive.Wait(timeout)) {
				throw new TimeoutException($"Read models did not go live within {timeout}.");
			}
		} catch (AggregateException ex) {
			throw new TimeoutException("A read model faulted before going live.", ex);
		}

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
		TrackedStreamListener[] tracked;
		lock (_tracked) { tracked = _tracked.ToArray(); }
		foreach (var listener in tracked) {
			if (string.IsNullOrEmpty(listener.StreamName)) { continue; } // not started yet
			var slice = inner.Connection.ReadStreamBackward(listener.StreamName, -1, 1);
			if (slice is null or StreamNotFoundSlice || slice.Events.Length == 0) { continue; }
			var target = slice.LastEventNumber;
			var delivered = listener.DeliveredThrough;
			if (delivered < target) {
				laggards.Add($"{listener.StreamName} delivered {delivered} of {target}");
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

	/// <summary>
	/// Exposes DeliveredThrough = max(startCheckpoint, lastDelivered), with lastDelivered
	/// recorded after the base publishes into the subscriber's queue — so DeliveredThrough >= N
	/// means "event N is in the read model's queue or applied", never "in flight". This fixes two
	/// ambiguities in the base <see cref="StreamListener.Position"/>: it advances at receipt
	/// (before the subscriber sees the event) and initializes to 0 (indistinguishable from
	/// "applied event 0").
	/// </summary>
	private sealed class TrackedStreamListener(
		string listenerName,
		IStreamStoreConnection connection,
		IStreamNameBuilder streamNameBuilder,
		IEventSerializer serializer)
		: StreamListener(listenerName, connection, streamNameBuilder, serializer) {
		private long _startCheckpoint = -1;
		private long _lastDelivered = -1;

		public long DeliveredThrough =>
			Math.Max(Interlocked.Read(ref _startCheckpoint), Interlocked.Read(ref _lastDelivered));

		public override void Start(
			string streamName,
			long? checkpoint = null,
			bool blockUntilLive = false,
			bool validateStream = false,
			CancellationToken cancelWaitToken = default) {
			Interlocked.Exchange(ref _startCheckpoint, checkpoint ?? -1);
			base.Start(streamName, checkpoint, blockUntilLive, validateStream, cancelWaitToken);
		}

		protected override void GotEvent(RecordedEvent recordedEvent) {
			base.GotEvent(recordedEvent); // deliver first, then record
			Interlocked.Exchange(ref _lastDelivered, recordedEvent.EventNumber);
		}
	}
}
