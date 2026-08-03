using ReactiveDomain.Util;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation;

public class ReadModelState {
	public readonly string ModelName;

	/// <summary>
	/// Positions of the streams the model reads itself. <see cref="SnapshotReadModel.Restore"/>
	/// starts a listener for each of these.
	/// </summary>
	public readonly List<Tuple<string, long>>? Checkpoints;

	/// <summary>
	/// Positions of streams fed to the model by something else — a relay, a shared category
	/// reader — that the model does not read itself. Restoring records them without starting a
	/// listener for any of them; it is the feeding source that resumes from these positions.
	/// Null when the model has no external sources, which is the shape a state has always had.
	/// </summary>
	public readonly List<Tuple<string, long>>? ExternalCheckpoints;

	public readonly object State;

	public ReadModelState(
		string modelName,
		List<Tuple<string, long>>? checkpoints,
		object state,
		List<Tuple<string, long>>? externalCheckpoints = null) {
		Ensure.NotNullOrEmpty(modelName, nameof(modelName));
		ModelName = modelName;
		Checkpoints = checkpoints;
		State = state;
		ExternalCheckpoints = externalCheckpoints;
	}
}
