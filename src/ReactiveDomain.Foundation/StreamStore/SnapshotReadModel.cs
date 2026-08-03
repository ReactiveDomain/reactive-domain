using ReactiveDomain.Util;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation;

public abstract class SnapshotReadModel : ReadModelBase {
	protected ReadModelState? StartingState { get; private set; }

	// Positions of sources the model does not read itself. Kept apart from the listener
	// checkpoints because nothing here may be started: a relay owns the read, and starting a
	// listener for it would double-deliver every event on that stream.
	private readonly Dictionary<string, long> _externalCheckpoints = new(StringComparer.Ordinal);

	protected SnapshotReadModel(
		string name,
		IConfiguredConnection connection)
		: base(name, connection) {
	}

	/// <summary>
	/// Restores the model from a snapshot: records the external checkpoints, applies the state,
	/// then starts a listener for each of the model's own checkpoints.
	/// </summary>
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
					_externalCheckpoints[external.Item1] = external.Item2;
				}
			}
		}
		ApplyState(StartingState);
		if (!startListeners || StartingState.Checkpoints == null)
			return;

		foreach (var stream in StartingState.Checkpoints) {
			Start(stream.Item1, stream.Item2, block, validateStreams, cancelWaitToken);
		}
	}

	/// <summary>
	/// Gets the recorded position of a source the model does not read itself.
	/// </summary>
	/// <param name="streamName">The external stream's name.</param>
	/// <param name="position">The recorded position, or -1 when none is recorded.</param>
	/// <returns>True when a position is recorded for that stream.</returns>
	protected bool TryGetExternalCheckpoint(string streamName, out long position) {
		Ensure.NotNullOrEmpty(streamName, nameof(streamName));
		lock (_externalCheckpoints) {
			if (_externalCheckpoints.TryGetValue(streamName, out position))
				return true;
		}
		position = -1;
		return false;
	}

	/// <summary>
	/// Records the position of a source the model does not read itself, so
	/// <see cref="GetExternalCheckpoints"/> carries it into the next snapshot. Replaces any
	/// position already recorded for that stream.
	/// </summary>
	/// <param name="streamName">The external stream's name.</param>
	/// <param name="position">The position reached on that stream.</param>
	protected void SetExternalCheckpoint(string streamName, long position) {
		Ensure.NotNullOrEmpty(streamName, nameof(streamName));
		lock (_externalCheckpoints) {
			_externalCheckpoints[streamName] = position;
		}
	}

	/// <summary>
	/// The positions of every external source, for <see cref="GetState"/> to pass to
	/// <see cref="ReadModelState"/>. Null when there are none, so a model without external
	/// sources emits exactly the state shape it always has.
	/// </summary>
	protected List<Tuple<string, long>>? GetExternalCheckpoints() {
		lock (_externalCheckpoints) {
			return _externalCheckpoints.Count == 0
				? null
				: _externalCheckpoints.Select(c => new Tuple<string, long>(c.Key, c.Value)).ToList();
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
