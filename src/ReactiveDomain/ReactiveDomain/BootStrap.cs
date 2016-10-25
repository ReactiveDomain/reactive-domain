using System.Reflection;
using NLog;

namespace ReactiveDomain
{
    public static class BootStrap
    {
        private static readonly Logger Log = NLog.LogManager.GetLogger("ReactiveDomain");
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
