// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation;

public class AggregateNotFoundException(Guid id, Type type)
	: Exception($"Aggregate '{id}' (type {type.Name}) was not found.") {
	public readonly Guid Id = id;
	public readonly Type Type = type;
}
