using System.Reflection;

using Microsoft.Extensions.Logging;

namespace ReactiveDomain.Foundation
{
    public static class BootStrap
    {
        private static readonly ILogger Log = Logging.LogProvider.GetLogger("ReactiveDomain");
        private static readonly string AssemblyName;
        static BootStrap()
        {
            var fullName = Assembly.GetExecutingAssembly().FullName;
            Log.LogInformation(fullName + " Loaded.");
            AssemblyName = fullName.Split(new[] { ',' })[0];

        }
        public static void Load()
        {
            Log.LogInformation(AssemblyName + " Loaded.");
        }
    }
}
