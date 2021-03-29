using System.Reflection;

namespace ReactiveDomain.Foundation
{
    public static class BootStrap
    {
        //TODO: Setup a static logger using LoggingAbstractions from Microsoft
        //private static readonly ILogger Log = LogManager.GetLogger("ReactiveDomain");
        private static readonly string AssemblyName;
        static BootStrap()
        {
            var fullName = Assembly.GetExecutingAssembly().FullName;
            //TODO: Setup a static logger using LoggingAbstractions from Microsoft
            //Log.Info(fullName + " Loaded.");
            AssemblyName = fullName.Split(new[] { ',' })[0];

        }
        public static void Load()
        {
            //TODO: Setup a static logger using LoggingAbstractions from Microsoft
            //Log.Info(AssemblyName + " Loaded.");
        }
    }
}
