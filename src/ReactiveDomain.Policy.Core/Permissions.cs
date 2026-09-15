using ReactiveDomain.Messaging;

namespace ReactiveDomain.Policy;

public static class Permissions {
	private static readonly Type _commandType = typeof(Command);

	public static Permission[] GetCommandPermissions(Type type) {
		return GetCommands(type).Select(t => new Permission(t)).ToArray();
	}

	public static IEnumerable<Type> GetCommands(Type type) {
		return type.GetNestedTypes().Where(t => _commandType.IsAssignableFrom(t));
	}
}
