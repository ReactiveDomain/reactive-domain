using System.Reflection;
using ReactiveDomain.Logging;

namespace ReactiveDomain.Foundation;

public static class BootStrap {
	private static readonly ILogger _log = LogManager.GetLogger("ReactiveDomain");
	private static readonly string _assemblyName;
	static BootStrap() {
		_assemblyName = Assembly.GetExecutingAssembly().GetName().FullName;
		_log.Info($"{_assemblyName} Loaded.");

	}
	public static void Load() {
		_log.Info($"{_assemblyName} Loaded.");
	}
}
