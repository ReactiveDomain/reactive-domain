// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation;

public class AggregateVersionException(Guid id, Type type, long aggregateVersion, long requestedVersion)
	: Exception(string.Format("Requested version {2} of aggregate '{0}' (type {1}) - aggregate version is {3}", id,
		type.Name, requestedVersion, aggregateVersion)) {
	public readonly Guid Id = id;
	public readonly Type Type = type;
	public readonly long AggregateVersion = aggregateVersion;
	public readonly long RequestedVersion = requestedVersion;
}
