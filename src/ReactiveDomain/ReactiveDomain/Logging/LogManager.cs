using System;
using NLog.Config;
using ReactiveDomain.Util;

namespace ReactiveDomain.Logging
{
    public static class LogManager
    {
        public static string LogsDirectory
        {
            get
            {
                if (!Initialized)
                    throw new InvalidOperationException("Init method must be called");
                return _logsDirectory;
            }
        }

        private static readonly ILogger GlobalLogger = GetLogger("GLOBAL-LOGGER");
        private static bool Initialized;
        private static Func<string, ILogger> LogFactory = x => new NLogger(x);
// ReSharper disable once InconsistentNaming
        internal static string _logsDirectory;

        static LogManager()
        {
            var conf = ConfigurationItemFactory.Default;
            conf.LayoutRenderers.RegisterDefinition("logsdir", typeof(NLogDirectoryLayoutRendered));
            conf.ConditionMethods.RegisterDefinition("is-dot-net", typeof(NLoggerHelperMethods).GetMethod("IsDotNet"));
            conf.ConditionMethods.RegisterDefinition("is-mono", typeof(NLoggerHelperMethods).GetMethod("IsMono"));
        }

        [Obsolete("Use GetLogger(string) and specify a component")]
        public static ILogger GetLoggerFor(Type type)
        {
            return GetLogger(type.Name);
        }

        [Obsolete("Use GetLogger(string) and specify a component")]
        public static ILogger GetLoggerFor<T>()
        {
            return GetLogger(typeof(T).Name);
        }

        public static ILogger GetLogger(string logName)
        {
            return new LazyLogger(() => LogFactory(logName));
        }

        public static void Init(string componentName, string logsDirectory)
        {
            Ensure.NotNull(componentName, "componentName");
            if (Initialized)
                throw new InvalidOperationException("Cannot initialize twice");

            Initialized = true;

            _logsDirectory = logsDirectory;
            Environment.SetEnvironmentVariable("EVENTSTORE_INT-COMPONENT-NAME", componentName, EnvironmentVariableTarget.Process);
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                var exc = e.ExceptionObject as Exception;
                if (exc != null)
                    GlobalLogger.FatalException(exc, "Global Unhandled Exception occurred.");
                else
                    GlobalLogger.Fatal("Global Unhandled Exception object: {0}.", e.ExceptionObject);
                GlobalLogger.Flush(TimeSpan.FromMilliseconds(500));
            };
        }

        public static void Finish()
        {
            try
            {
                GlobalLogger.Flush();
                NLog.LogManager.Configuration = null;
            }
            catch (Exception exc)
            {
                GlobalLogger.ErrorException(exc, "Exception during flushing logs, ignoring...");
            }
        }

        public static void SetLogFactory(Func<string, ILogger> factory)
        {
            Ensure.NotNull(factory, "factory");
            LogFactory = factory;
        }
    }
}
