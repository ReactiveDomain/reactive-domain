namespace ReactiveDomain.Foundation.Tests.StreamListenerTests.Common;

/// <summary>
/// Generate stream names for testing. 
/// </summary>
internal class TestStreamNameBuilder(Guid testRunGuid) : IStreamNameBuilder {
	public string GenerateForAggregate(Type type, Guid id) {
		return $"{type.Name}-{id:N}{testRunGuid:N}";
	}

	public string GenerateForCategory(Type type) {
		//mock category stream, can't use $ here
		return $"ce-{type.Name}{testRunGuid:N}";
	}

	public string GenerateForEventType(string type) {
		//mock event type stream, can't use $ here
		return $"et-{type}{testRunGuid:N}";
	}
}
