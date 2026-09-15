using System.Reflection;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation;

/// <summary>
/// Settings object for the JsonMessageSerializer
/// </summary>
public class JsonMessageSerializerSettings {

	/// <summary>
	/// Instructs the Serializer to write the fully qualified Assembly name and version.
	/// N.B. This can cause types and not to be found if the assembly version changes even if the type has not changed.
	/// </summary>
	public readonly bool FullyQualify;
	/// <summary>
	/// Replaces the recorded assembly when deserializing
	/// </summary>
	public readonly Assembly? AssemblyOverride;
	/// <summary>
	/// Serializer throws if a target type cannot be found, rather than returning a JObject that every
	/// listener then silently skips. Defaults to true: the silent path is opt-in for tooling.
	/// </summary>
	public readonly bool ThrowOnTypeNotFound;
	/// <summary>
	/// Create a new settings object for the JsonMessageSerializer
	/// </summary>
	/// <param name="fullyQualify">N.B. Instructs the Serializer to write the fully qualified Assembly name and version. This can cause types and not to be found if the assembly version changes even if the type has not changed.</param>
	/// <param name="assemblyOverride">Replaces the recorded assembly when deserializing</param>
	/// <param name="throwOnTypeNotFound">Throws if a target type cannot be found, rather than returning a JObject. Defaults to true.</param>
	public JsonMessageSerializerSettings(
		bool fullyQualify = false,
		Assembly? assemblyOverride = null,
		bool throwOnTypeNotFound = true) {
		FullyQualify = fullyQualify;
		AssemblyOverride = assemblyOverride;
		ThrowOnTypeNotFound = throwOnTypeNotFound;
	}
}
