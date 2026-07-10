using System.Reflection;
using ReactiveDomain.Logging;

namespace ReactiveDomain.Messaging;

//this class is to help force assembly loading when building the message hierarchy
public static class BootStrap {
	private static readonly ILogger _log = LogManager.GetLogger("ReactiveDomain.Messaging");
	private static readonly string _assemblyName;
	static BootStrap() {
		_assemblyName = Assembly.GetExecutingAssembly().GetName().FullName;
		_log.Info($"{_assemblyName} Loaded.");
	}
	public static void Load() {
		_log.Info($"{_assemblyName} Configured.");
	}
	public static void Configure() {
		_log.Info($"{_assemblyName} Configured.");
	}
}
