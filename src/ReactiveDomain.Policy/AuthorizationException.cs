namespace ReactiveDomain.Policy;

public class AuthorizationException(Type command, string? message)
	: Exception($"{command.Name} not authorized {message}") {
	public Type Command { get; } = command;
}
