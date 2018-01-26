using System.Reflection;
using ReactiveDomain.Core.Logging;

namespace ReactiveDomain.Messaging
{
    public static class BootStrap
    {
        private static readonly ILogger Log = LogManager.GetLogger("ReactiveDomain.Messaging");
        private static readonly string AssemblyName;
        static BootStrap()
        {
            var fullName = Assembly.GetExecutingAssembly().FullName;
            Log.Info(fullName + " Loaded.");
            AssemblyName = fullName.Split(new[] { ',' })[0];

        }
        public static void Load()
        {
            Log.Info(AssemblyName + " Configured.");
        }
        public static void Configure()
        {
            Log.Info(AssemblyName + " Configured.");
        }
    }
}
