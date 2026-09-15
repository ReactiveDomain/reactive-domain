using ReactiveDomain.Logging;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation;

/// <summary>
/// A read model that writes to an external store in batches: nothing while catching up, one flush
/// when live, then at most one flush per event.
/// </summary>
/// <remarks>
/// <para>Handlers record changes on buffers from <see cref="CreateBuffer{TKey,TModel}"/> instead of
/// writing. <see cref="Flush"/> is called with the checkpoints of exactly what has been applied, so a
/// store that commits the rows and the checkpoints in one transaction is never ahead of or behind
/// itself — which is what makes the checkpoints it saves safe to resume from, and safe for another
/// model to bound its own resume by (<see cref="StreamCheckpoint.BoundedBy(StreamCheckpoint?)"/>).</para>
/// <para>Hydrating the store's contents into memory before starting, and the writes themselves, stay
/// with the derived class: which tables and what shape are its business.</para>
/// <para>Flush runs on the queue thread. It runs at the live transition when anything is pending,
/// and after each event handled while no stream is reading that left something pending; never with
/// nothing to write. It is skipped while a later-started stream is reading, so that stream's history
/// is batched like the first was.</para>
/// <para>A throw from the live-transition flush leaves <see cref="ReadModelBase.IsLive"/> pending
/// and retries that write on this model's queue three times, the same budget a dropped
/// subscription uses. It does not wait for another stream event, and it does not report live over a
/// store that is not live. A third failed retry faults <see cref="ReadModelBase.IsLive"/> with the last
/// exception. A throw from a per-event flush is logged by the queue. In both cases the buffers keep
/// what they held before the fault. Dispose cancels a transition that is still retrying.</para>
/// <para><see cref="Flush"/> must finish the write before it returns. The buffers are cleared on
/// return, and <see cref="WriteBuffer{TKey,TModel}.Upserts"/> / <see cref="WriteBuffer{TKey,TModel}.Deletes"/>
/// are copies, so a flush that retains them still has what it wrote. An async or fire-and-forget
/// flush that returns before the store has the rows persists nothing.</para>
/// </remarks>
public abstract class BufferedReadModelBase : ReadModelBase {
	private static readonly ILogger Log = LogManager.GetLogger("ReactiveDomain");
	private const int MaxTransitionRetries = 3;
	private static readonly int[] RetryBackoffMs = [0, 50, 200];

	private readonly List<IWriteBuffer> _buffers = [];
	// Queue thread only: set in AtLiveTransition, read in AfterDispatch, both of which run there.
	private bool _storeLive;
	private int _retryQueued;
	private int _retryCount;

	/// <inheritdoc cref="ReadModelBase(string, IConfiguredConnection)"/>
	protected BufferedReadModelBase(string name, IConfiguredConnection connection) : base(name, connection) { }

	/// <summary>
	/// Declares a buffer this model writes through. Create one per kind of row — table, collection,
	/// document type — and hold it in a field for the handlers and <see cref="Flush"/> to use.
	/// </summary>
	/// <param name="comparer">How keys are compared; the type's default when null.</param>
	protected WriteBuffer<TKey, TModel> CreateBuffer<TKey, TModel>(IEqualityComparer<TKey>? comparer = null)
		where TKey : notnull {
		var buffer = new WriteBuffer<TKey, TModel>(comparer);
		lock (_buffers) {
			_buffers.Add(buffer);
		}
		return buffer;
	}

	/// <summary>True while any buffer holds something to write.</summary>
	protected bool HasPendingWrites {
		get {
			lock (_buffers) {
				return _buffers.Any(b => b.HasPending);
			}
		}
	}

	/// <summary>
	/// Writes everything the buffers hold, and <paramref name="checkpoints"/> beside it.
	/// </summary>
	/// <param name="checkpoints">
	/// Where each stream stands once the buffered changes are applied — <see cref="ReadModelBase.AppliedCheckpoints"/>,
	/// exact. Persist them in the same transaction as the rows.
	/// </param>
	/// <remarks>
	/// Runs on the queue thread under <see cref="ReadModelBase.ReaderLock"/>, so it must not wait on
	/// this model. The buffers are cleared after it returns; on a throw they are left as they were,
	/// and the same rows are handed to the next attempt, so this write must be safe to repeat.
	/// </remarks>
	protected abstract void Flush(IReadOnlyList<StreamCheckpoint> checkpoints);

	internal override void AtLiveTransition() {
		// Set first: a flush that throws here must not leave the model buffering forever.
		_storeLive = true;
		FlushPending();
	}

	internal override bool AdvanceLiveTransition() {
		try {
			AtLiveTransition();
			_retryCount = 0;
			return true;
		} catch (Exception ex) {
			OnTransitionFlushFailed(ex);
			return false;
		}
	}

	internal override void RetryTransition() {
		Interlocked.Exchange(ref _retryQueued, 0);
		if (!NoStreamsPending || IsLive.IsCompleted)
			return;
		lock (ReaderLock) {
			try {
				if (!FlushPending())
					return;
			} catch (Exception ex) {
				OnTransitionFlushFailed(ex);
				return;
			}
		}
		_retryCount = 0;
		CompleteLiveTransition();
	}

	internal override void AfterDispatch() {
		if (!_storeLive || !NoStreamsPending)
			return;
		try {
			if (!FlushPending())
				return;
		} catch (Exception ex) {
			if (!IsLive.IsCompleted)
				OnTransitionFlushFailed(ex);
			throw;
		}
		CompleteLiveTransition();
	}

	private void OnTransitionFlushFailed(Exception ex) {
		Log.ErrorException(ex, "{0} live-transition flush failed ({1}/{2}).",
			GetType().Name, _retryCount, MaxTransitionRetries);
		if (_retryCount >= MaxTransitionRetries) {
			_storeLive = false;
			FailLiveTransition(ex);
			return;
		}
		ScheduleTransitionRetry();
	}

	private void ScheduleTransitionRetry() {
		if (Interlocked.CompareExchange(ref _retryQueued, 1, 0) != 0)
			return;
		var delay = RetryBackoffMs[Math.Min(_retryCount, RetryBackoffMs.Length - 1)];
		_retryCount++;
		if (delay == 0) {
			EnqueueTransitionRetry();
			return;
		}
		_ = Task.Run(async () => {
			try {
				await Task.Delay(delay).ConfigureAwait(false);
				EnqueueTransitionRetry();
			} catch {
				Interlocked.Exchange(ref _retryQueued, 0);
			}
		});
	}

	private bool FlushPending() {
		if (!HasPendingWrites)
			return false;
		Flush(AppliedCheckpoints);
		lock (_buffers) {
			_buffers.ForEach(b => b.Clear());
		}
		return true;
	}
}
