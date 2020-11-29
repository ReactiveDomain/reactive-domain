using System;
using System.Reflection;
using ReactiveDomain.Foundation;
using ReactiveDomain.Identity.Storage.Domain.Services;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Logging;

namespace ReactiveDomain.Identity.Storage
{
    /// <summary>
    /// The bootstrapper for Elbe. Use this to configure Elbe as a library or to run it as an application.
    /// </summary>
    public class Bootstrap
    {
        internal const string LogName = "PKI-Elbe";
        private static string _assemblyName;
        private static readonly ILogger Log = LogManager.GetLogger(LogName);

        private static IStreamStoreConnection _esConnection;
        private static StreamStoreRepository _repo;

        private static UserSvc _userSvc;
        private static RoleSvc _roleSvc;
        private static ApplicationSvc _applicationSvc;
        private static ApplicationConfigurationSvc _applicationConfigurationSvc;
        public static string Schema = "pki_elbe";
        public static Func<string, IListener> _getListener;

        /// <summary>
        /// Create an Elbe Bootstrap instance.
        /// </summary>
        public Bootstrap()
        {
            var fullName = Assembly.GetExecutingAssembly().FullName;
            Log.Info(fullName + " Created.");
            _assemblyName = fullName.Split(',')[0];
        }

        /// <summary>
        /// Load the assembly.
        /// </summary>
        public static void Load()
        {
            var fullName = Assembly.GetExecutingAssembly().FullName;
            _assemblyName = fullName.Split(',')[0];
            Log.Info(_assemblyName + " Loaded.");
        }

        /// <summary>
        /// Configure the Elbe library for use as a DLL by another application.
        /// </summary>
        /// <param name="esConnection">An EventStore connection</param>
        /// <param name="bus">The dispatcher to which services should subscribe</param>
        public static void Configure(
            IStreamStoreConnection esConnection,
            IDispatcher bus
        )
        {
            _esConnection = esConnection;
            _repo = new StreamStoreRepository(
                            new PrefixedCamelCaseStreamNameBuilder(Schema),
                            esConnection,
                            new JsonMessageSerializer());
            

            _getListener = (string s) => new QueuedStreamListener(nameof(s), 
                                        _esConnection,
                                        new PrefixedCamelCaseStreamNameBuilder(Schema),
                                        new JsonMessageSerializer());
            
           
            _userSvc = new UserSvc(_repo, bus,_getListener);
            _roleSvc = new RoleSvc(_repo, bus,_getListener);
            _applicationSvc = new ApplicationSvc(_repo, bus,_getListener);
            _applicationConfigurationSvc = new ApplicationConfigurationSvc(bus,_getListener);
        }

       

        /// <summary>
        /// Shut down services and connections.
        /// </summary>
        public static void Shutdown()
        {
            _userSvc?.Dispose();
            _roleSvc?.Dispose();
            _applicationSvc?.Dispose();
            _applicationConfigurationSvc?.Dispose();
            _esConnection?.Close();
        }
    }
}
