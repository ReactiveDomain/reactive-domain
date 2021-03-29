using System.Reflection;

namespace ReactiveDomain.Messaging
{
    //this class is to help force assembly loading when building the message hierarchy
    public static class BootStrap
    {
        //TODO: Setup a static logger using LoggingAbstractions from Microsoft
        //private static readonly ILogger Log = LogManager.GetLogger("ReactiveDomain.Messaging");
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
            //Log.Info(AssemblyName + " Configured.");
        }
        public static void Configure()
        {
            //TODO: Setup a static logger using LoggingAbstractions from Microsoft
            //Log.Info(AssemblyName + " Configured.");
        }
    }
}
