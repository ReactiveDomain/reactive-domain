namespace ReactiveDomain;

public class UnknownTypeException(string typeName)
	: Exception($"TypeName'{typeName}' was not found in the currently loaded appdomains.");
