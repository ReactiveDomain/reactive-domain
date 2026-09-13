namespace ReactiveDomain.Foundation;

public static class RepositoryExtensions {
	/// <inheritdoc cref="IRepository.Save"/>
	public static StreamCheckpoint Save(this IRepository repository, IEventSource aggregate) {
		return repository.Save(aggregate);
	}
}
