using System.Reflection;
using ReactiveDomain.Logging;

namespace ReactiveDomain.Foundation
{
    public static class BootStrap
    {
        private static readonly ILogger Log = LogManager.GetLogger("ReactiveDomain");
        private static readonly string AssemblyName;
        static BootStrap()
        {
            var fullName = Assembly.GetExecutingAssembly().FullName;
            Log.Info(fullName + " Loaded.");
            AssemblyName = fullName.Split(new[] { ',' })[0];

        }
        public static void Load()
        {
            Log.Info(AssemblyName + " Loaded.");
        }
    }
}
