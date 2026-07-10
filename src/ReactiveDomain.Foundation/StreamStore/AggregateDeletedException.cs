// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation;

public class AggregateDeletedException(Guid id, Type type)
	: Exception($"Aggregate '{id}' (type {type.Name}) was deleted.") {
	public readonly Guid Id = id;
	public readonly Type Type = type;
}
