namespace ReactiveDomain.Policy;

/// <summary>
/// An attempt was made to add a duplicate application to the system.
/// </summary>
public class DuplicateApplicationException(string appName, string securityModelVersion)
	: Exception($"Application {appName} with version {securityModelVersion} already exists.");
