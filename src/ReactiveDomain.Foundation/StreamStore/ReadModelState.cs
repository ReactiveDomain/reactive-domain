using ReactiveDomain.Util;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation;

public class ReadModelState {
	public readonly string ModelName;

	/// <summary>
	/// Checkpoints for the streams the model reads itself. <see cref="SnapshotReadModel.Restore"/>
	/// starts a listener for each of these, resuming from its <see cref="StreamCheckpoint.Version"/>.
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
