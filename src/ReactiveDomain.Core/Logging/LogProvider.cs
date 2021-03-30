using System.Collections.Generic;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ReactiveDomain.Logging
{
    public static class LogProvider
    {
        private static IDictionary<string, ILogger> _loggers = new Dictionary<string, ILogger>();
        private static ILoggerFactory _loggerFactory;

        /// <summary>
        /// This MUST be set during program startup.
        /// </summary>
        /// <param name="factory"></param>
        public static void SetLogFactory(ILoggerFactory factory)
        {
            _loggerFactory?.Dispose();
            _loggerFactory = factory;
            _loggers.Clear();
        }

        public static ILogger GetLogger(string category)
        {
            if (!_loggers.ContainsKey(category))
            {
                _loggers[category] = _loggerFactory?.CreateLogger(category) ?? NullLogger.Instance;
            }
            return _loggers[category];
        }

        public static ILogger GetLogger<T>() => GetLogger(typeof(T).Name);
    }
}