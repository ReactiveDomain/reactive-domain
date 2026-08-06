using System.Diagnostics.CodeAnalysis;
using ReactiveDomain.Messaging;
using ReactiveDomain.Util;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation;

public abstract class SnapshotReadModel : ReadModelBase {
	protected ReadModelState? StartingState { get; private set; }

	// Kept apart from the listener checkpoints: merging them would start a listener for a stream a
	// relay already reads, and deliver every event on it twice.
	private readonly Dictionary<string, StreamCheckpoint> _externalCheckpoints = new(StringComparer.Ordinal);

	protected SnapshotReadModel(
		string name,
		IConfiguredConnection connection)
		: base(name, connection) {
	}

	/// <summary>Restores the model from a snapshot.</summary>
	/// <param name="snapshot">The state to restore from.</param>
	/// <param name="startListeners">Start a listener for each entry in
	/// <see cref="ReadModelState.Checkpoints"/>. Never starts one for an entry in
	/// <see cref="ReadModelState.ExternalCheckpoints"/> — those streams are read elsewhere.</param>
	/// <param name="block">Block until each started listener is live.</param>
	/// <param name="validateStreams">Ensure each started stream exists.</param>
	/// <param name="cancelWaitToken">Cancels waiting when <paramref name="block"/> is true.</param>
	protected virtual void Restore(
		ReadModelState snapshot,
		bool startListeners = true,
		bool block = false,
		bool validateStreams = false,
		CancellationToken cancelWaitToken = default) {
		if (StartingState != null) {
			throw new InvalidOperationException("ReadModel has already been restored.");
		}
		Ensure.NotNull(snapshot, nameof(snapshot));
		StartingState = snapshot;
		// Recorded before ApplyState so a model can read its sources' positions while restoring
		// — that is when it has to tell its relay where to resume from.
		if (snapshot.ExternalCheckpoints != null) {
			lock (_externalCheckpoints) {
				foreach (var external in snapshot.ExternalCheckpoints) {
					_externalCheckpoints[external.StreamName] = external;
				}
			}
		}
		ApplyState(StartingState);
		// ApplyState is the one place DirectApply is expected: rebuilding state from a snapshot is not
		// input from outside, it is the snapshot's own, and the checkpoints restored alongside it
		// describe exactly that. Anything applied after this is unaccounted for again.
		MarkInputReplayable();
		if (!startListeners || StartingState.Checkpoints == null)
			return;

		foreach (var stream in StartingState.Checkpoints) {
			// A null version passes straight through as "no checkpoint", which starts the stream from
			// its first event — the only reading of a checkpoint that never delivered one that does
			// not skip that event.
			Start(stream.StreamName, stream.Version, block, validateStreams, cancelWaitToken);
		}
	}

	/// <summary>The recorded checkpoint of an external source.</summary>
	/// <param name="streamName">The external stream's name.</param>
	/// <param name="checkpoint">The recorded checkpoint, or null when none is recorded.</param>
	/// <returns>True when a checkpoint is recorded for that stream.</returns>
	protected bool TryGetExternalCheckpoint(
		string streamName,
		[NotNullWhen(true)] out StreamCheckpoint? checkpoint) {
		Ensure.NotNullOrEmpty(streamName, nameof(streamName));
		lock (_externalCheckpoints) {
			if (_externalCheckpoints.TryGetValue(streamName, out var recorded)) {
				checkpoint = recorded;
				return true;
			}
		}
		checkpoint = null;
		return false;
	}

	/// <summary>
	/// Records how far an external source has reached, for the next snapshot to carry. Replaces any
	/// checkpoint already recorded for that stream.
	/// </summary>
	/// <param name="streamName">The external stream's name.</param>
	/// <param name="version">The version reached on that stream.</param>
	/// <param name="position">
	/// That event's <c>$all</c> position, when the feeding source has one. Supplying it is what makes
	/// the checkpoint comparable against other streams' — see <see cref="StreamCheckpoint"/>.
	/// </param>
	protected void SetExternalCheckpoint(string streamName, long version, Position? position = null) {
		Ensure.NotNullOrEmpty(streamName, nameof(streamName));
		lock (_externalCheckpoints) {
			_externalCheckpoints[streamName] = new StreamCheckpoint(streamName, version, position);
		}
		// Input that arrived through DirectApply or Publish now has somewhere to be replayed from,
		// which is what makes the model snapshottable again.
		MarkInputReplayable();
	}

	/// <summary>
	/// For <see cref="GetState"/> to pass to <see cref="ReadModelState"/>. Null when there are none,
	/// so a model without external sources emits the state shape it always has.
	/// </summary>
	protected List<StreamCheckpoint>? GetExternalCheckpoints() {
		lock (_externalCheckpoints) {
			return _externalCheckpoints.Count == 0
				? null
				: _externalCheckpoints.Values.ToList();
		}
	}

	protected abstract void ApplyState(ReadModelState snapshot);

	/// <summary>
	/// The model's state and checkpoints, read on the calling thread.
	/// </summary>
	/// <remarks>
	/// Under live traffic the two are read at different moments, so the checkpoints can name events the
	/// state does not yet contain — restoring such a snapshot resumes past them and never applies them.
	/// Use <see cref="CaptureConsistentState"/> for a snapshot to persist; this remains for reading a
	/// model that is quiet, and for supplying the state that capture pairs with a checkpoint.
	/// </remarks>
	public abstract ReadModelState GetState();

	/// <summary>
	/// Captures a snapshot whose checkpoints describe exactly the events built into its state.
	/// </summary>
	/// <returns>
	/// <see cref="GetState"/>'s result with its <see cref="ReadModelState.Checkpoints"/> replaced by the
	/// ones true at the point the state was read. Cancelled if the model is disposed while capturing.
	/// </returns>
	/// <exception cref="InvalidOperationException">
	/// The model has input no checkpoint accounts for — see <see cref="HasUnreplayableInput"/>.
	/// </exception>
	/// <remarks>
	/// <see cref="GetState"/> runs on the queue thread, so an implementation of it must not block or
	/// wait on this model. The checkpoints it collects itself are discarded in favour of the captured
	/// ones; everything else it returns is kept.
	/// </remarks>
	public Task<ReadModelState> CaptureConsistentState() {
		if (HasUnreplayableInput) {
			throw new InvalidOperationException(
				$"{GetType().Name} has had messages applied through DirectApply or Publish that no " +
				"checkpoint accounts for, so a snapshot of it could not be restored. A model fed from " +
				"outside its own listeners must record where that input came from — see " +
				$"{nameof(SetExternalCheckpoint)}.");
		}
		return ReadAtConsistentCut(checkpoints => {
			var state = GetState();
			return new ReadModelState(
				state.ModelName,
				checkpoints.ToList(),
				state.State,
				state.ExternalCheckpoints);
		});
	}

	/// <summary>
	/// True once a message has reached this model through <see cref="DirectApply"/> or
	/// <see cref="Publish"/> without anything recording where it came from, which makes the model's
	/// state unreachable by replay and so unsafe to snapshot.
	/// </summary>
	/// <remarks>
	/// <para>A checkpoint describes what the model's listeners delivered. Input from anywhere else is
	/// in the state and in no checkpoint, so restoring would silently produce a different model — not
	/// a stale one, a wrong one. <see cref="CaptureConsistentState"/> refuses rather than hand back a
	/// snapshot with that in it.</para>
	/// <para>Cleared by <see cref="SetExternalCheckpoint"/>: a relay that says where its input came
	/// from has made it replayable, which is the whole point of an external checkpoint. A model that
	/// feeds itself and records nothing stays unsnapshottable, which is correct.</para>
	/// </remarks>
	public bool HasUnreplayableInput { get; private set; }

	/// <summary>
	/// Records that input reaching this model from outside its listeners has been accounted for, and
	/// can be replayed from what the next snapshot will carry.
	/// </summary>
	protected void MarkInputReplayable() => HasUnreplayableInput = false;

	/// <inheritdoc cref="ReadModelBase.DirectApply"/>
	/// <remarks>
	/// No listener saw this, so no checkpoint will describe it: this sets
	/// <see cref="HasUnreplayableInput"/> until something records where the message came from.
	/// </remarks>
	public override void DirectApply(IMessage message) {
		HasUnreplayableInput = true;
		base.DirectApply(message);
	}

	/// <inheritdoc cref="ReadModelBase.Publish"/>
	/// <remarks>
	/// No listener saw this, so no checkpoint will describe it: this sets
	/// <see cref="HasUnreplayableInput"/> until something records where the message came from.
	/// </remarks>
	public override void Publish(IMessage message) {
		HasUnreplayableInput = true;
		base.Publish(message);
	}

	private bool _disposed;
	protected override void Dispose(bool disposing) {
		if (_disposed)
			return;
		_disposed = true;
		if (disposing) {

		}
		base.Dispose(disposing);
	}

}
