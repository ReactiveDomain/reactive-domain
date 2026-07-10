using ReactiveDomain.Util;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation;

public class ReadModelState {
	public readonly string ModelName;
	public readonly List<Tuple<string, long>>? Checkpoints;
	public readonly object State;

	public ReadModelState(
		string modelName,
		List<Tuple<string, long>>? checkpoints,
		object state) {
		Ensure.NotNullOrEmpty(modelName, nameof(modelName));
		ModelName = modelName;
		Checkpoints = checkpoints;
		State = state;
	}
}
