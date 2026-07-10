using ReactiveDomain.Util;

namespace ReactiveDomain.Logging;

public static class LogManager {

	// ReSharper disable  InconsistentNaming
	private static readonly ILogger GlobalLogger = GetLogger("GLOBAL-LOGGER");
	public static Func<string, ILogger>? LogFactory { get; private set; }
	public static bool Initialized { get; private set; }

	static LogManager() { }

	public static ILogger GetLogger(string logName) {
		LogFactory ??= _ => new NullLogger();
		return new LazyLogger(() => LogFactory(logName));
	}

	public static void Init(string componentName) {
		Ensure.NotNull(componentName, "componentName");
		if (Initialized)
			throw new InvalidOperationException("Cannot initialize twice");
		Initialized = true;

		AppDomain.CurrentDomain.UnhandledException += (_, e) => {
			if (e.ExceptionObject is Exception exc)
				GlobalLogger.FatalException(exc, "Global Unhandled Exception occurred.");
			else
				GlobalLogger.Fatal("Global Unhandled Exception object: {0}.", e.ExceptionObject);
			GlobalLogger.Flush(TimeSpan.FromMilliseconds(500));
		};
	}
	public static void Finish() {
		try {
			GlobalLogger.Flush();
		} catch (Exception exc) {
			GlobalLogger.ErrorException(exc, "Exception during flushing logs, ignoring...");
		}
	}

	public static void SetLogFactory(Func<string, ILogger> factory) {
		Ensure.NotNull(factory, "factory");
		LogFactory = factory;
	}
}
