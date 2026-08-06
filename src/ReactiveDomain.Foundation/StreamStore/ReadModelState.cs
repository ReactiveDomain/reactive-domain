using ReactiveDomain.Util;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation;

/// <summary>
/// A read model's state and the checkpoints it was taken at. Persisting one is the caller's job:
/// nothing here serializes itself.
/// </summary>
public class ReadModelState {
	public readonly string ModelName;

	/// <summary>
	/// Checkpoints for the streams the model reads itself. <see cref="SnapshotReadModel.Restore"/>
	/// starts a listener for each of these, resuming after its <see cref="StreamCheckpoint.Version"/>
	/// — or from the beginning of the stream when that is null, which is what is recorded by a stream that had
	/// delivered nothing when the snapshot was taken.
	/// </summary>
	public readonly List<StreamCheckpoint>? Checkpoints;

	/// <summary>
	/// Checkpoints for streams fed to the model by something else — a relay, a shared category
	/// reader — that the model does not read itself. Restoring records them without starting a
	/// listener for any of them; it is the feeding source that resumes from these positions.
	/// Null when the model has no external sources.
	/// </summary>
	public readonly List<StreamCheckpoint>? ExternalCheckpoints;

	public readonly object State;

	/// <summary>
	/// How the events built into this snapshot stand to those built into another: whether it is the
	/// same, earlier, later, or neither.
	/// </summary>
	/// <param name="other">The snapshot to compare against.</param>
	/// <remarks>
	/// <para>Spans <see cref="Checkpoints"/> and <see cref="ExternalCheckpoints"/> together — a relay
	/// having fed more of a stream is later, however the stream reached the model.</para>
	/// <para>Meaningful between snapshots of one model. See <see cref="StreamCheckpoint.Compare"/> for
	/// why comparing two different models' snapshots is not, and why the answer can be
	/// <see cref="CheckpointOrder.Concurrent"/>.</para>
	/// </remarks>
	public CheckpointOrder Compare(ReadModelState other) {
		Ensure.NotNull(other, nameof(other));
		return StreamCheckpoint.Compare(Covered(this), Covered(other));

		static IEnumerable<StreamCheckpoint> Covered(ReadModelState state) =>
			(state.Checkpoints ?? []).Concat(state.ExternalCheckpoints ?? []);
	}

	public ReadModelState(
		string modelName,
		List<StreamCheckpoint>? checkpoints,
		object state,
		List<StreamCheckpoint>? externalCheckpoints = null) {
		Ensure.NotNullOrEmpty(modelName, nameof(modelName));
		ModelName = modelName;
		Checkpoints = checkpoints;
		State = state;
		ExternalCheckpoints = externalCheckpoints;
	}
}
