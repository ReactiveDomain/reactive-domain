using System.Reflection;

using Microsoft.Extensions.Logging;

namespace ReactiveDomain.Messaging
{
    //this class is to help force assembly loading when building the message hierarchy
    public static class BootStrap
    {
        private static readonly ILogger Log = Logging.LogProvider.GetLogger("ReactiveDomain.Messaging");
        private static readonly string AssemblyName;
        static BootStrap()
        {
            var fullName = Assembly.GetExecutingAssembly().FullName;
            Log.LogInformation(fullName + " Loaded.");
            AssemblyName = fullName.Split(new[] { ',' })[0];

        }
        public static void Load()
        {
            Log.LogInformation(AssemblyName + " Configured.");
        }
        public static void Configure()
        {
            Log.LogInformation(AssemblyName + " Configured.");
        }
    }
}
