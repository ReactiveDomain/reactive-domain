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
/// <para>A throw from the live-transition flush faults <see cref="ReadModelBase.IsLive"/> — and so
/// <see cref="ReadModelBase.StartAllAsync"/> — rather than reporting live over a store that is not. A throw
/// from a per-event flush is logged by the queue. In both cases the buffers keep what they held, and
/// the next flush attempts to write it along with whatever has accumulated since.</para>
/// </remarks>
public abstract class BufferedReadModelBase : ReadModelBase {
	private readonly List<IWriteBuffer> _buffers = [];
	// Queue thread only: set in AtLiveTransition, read in AfterDispatch, both of which run there.
	private bool _storeLive;

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
	/// this model. The buffers are cleared after it returns; on a throw they are left as they were.
	/// </remarks>
	protected abstract void Flush(IReadOnlyList<StreamCheckpoint> checkpoints);

	internal override void AtLiveTransition() {
		// Set first: a flush that throws here must not leave the model buffering forever.
		_storeLive = true;
		FlushPending();
	}

	internal override void AfterDispatch() {
		if (_storeLive && NoStreamsPending)
			FlushPending();
	}

	private void FlushPending() {
		if (!HasPendingWrites)
			return;
		Flush(AppliedCheckpoints);
		lock (_buffers) {
			_buffers.ForEach(b => b.Clear());
		}
	}
}
