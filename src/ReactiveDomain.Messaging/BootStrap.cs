using System.Reflection;
using ReactiveDomain.Logging;

namespace ReactiveDomain.Messaging
{
    //this class is to help force assembly loading when building the message hierarchy
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
