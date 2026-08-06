using System.Diagnostics.CodeAnalysis;
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
		if (!startListeners || StartingState.Checkpoints == null)
			return;

		foreach (var stream in StartingState.Checkpoints) {
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

	public abstract ReadModelState GetState();

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
