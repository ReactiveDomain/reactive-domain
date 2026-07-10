namespace ReactiveDomain.IdentityStorage;

/// <summary>
/// An attempt was made to add a duplicate subject to the system.
/// </summary>
public class DuplicateSubjectException(string authProvider, string authDomain, string userName)
	: Exception($"User {authDomain}\\{userName} with provider {authProvider} already exists.");
