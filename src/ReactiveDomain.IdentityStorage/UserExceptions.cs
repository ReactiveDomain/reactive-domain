namespace ReactiveDomain.IdentityStorage;

/// <summary>
/// An attempt was made to add a duplicate user to the system.
/// </summary>
public class DuplicateUserException(Guid id, string fullName, string email)
	: Exception($"User {id}: {fullName}\\{email} already exists.");

/// <summary>
/// Throw this exception when a user lookup returns no results.
/// </summary>
public class UserNotFoundException(string message) : Exception(message);

/// <summary>
/// Throw this exception when a user lookup returns a user but that user is deactivated.
/// </summary>
public class UserDeactivatedException(string message) : Exception(message);

public class DuplicateClientException(Guid id, string name) : Exception($"Client {name} with ID {id} already exists.");
