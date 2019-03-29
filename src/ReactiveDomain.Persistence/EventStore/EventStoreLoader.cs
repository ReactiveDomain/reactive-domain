using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using EventStore.ClientAPI;
using Newtonsoft.Json;
using ReactiveDomain.Logging;
using ReactiveDomain.Util;
using ILogger = ReactiveDomain.Logging.ILogger;


namespace ReactiveDomain.EventStore {
    public class EventStoreLoader {

        /// <summary>
        /// Options when there is a process conflict when the EventStore process starts
        /// </summary>
        public enum StartConflictOption {
            Kill,
            Connect,
            Error
        }

        private readonly ILogger _log = LogManager.GetLogger("Common");
        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None };
        private Process _process;
        private bool _disposed;

        public IStreamStoreConnection Connection { get; private set; }

        /// <summary>
        /// Setup EventStore based on the connection settings provided by the caller
        /// </summary>
        /// <remarks>
        /// <see cref="StreamStoreConnectionSettings"/> and <see cref="StreamStoreConnectionSettingsBuilder"/> for options to configure:
        /// <list type="bullet">  
        ///     <item>  
        ///         <description>A single server using an IPAddress and TCP Port</description>  
        ///     </item>  
        ///     <item>  
        ///         <description>A cluster using a DNS name and HTTP Port</description>  
        ///     </item>  
        ///     <item>  
        ///         <description>A cluster using one or more gossip seed IPAddresses and an HTTP Port</description>  
        ///     </item>  
        /// </list>  
        /// </remarks>
        /// <param name="connectionSettings"><see cref="StreamStoreConnectionSettings"/> defined by the caller.</param>
        public EventStoreLoader(StreamStoreConnectionSettings connectionSettings)
        {
            if (connectionSettings.IsSingleConnection) {
                Connect(connectionSettings.UserCredentials, connectionSettings.SingleServerIpEndPoint, connectionSettings.VerboseLogging);
            } else if (connectionSettings.IsDnsClusterConnection) {
                Connect(connectionSettings.UserCredentials, connectionSettings.ClusterDns, connectionSettings.NetworkIpPort, connectionSettings.VerboseLogging);
            } else if (connectionSettings.IsGossipSeedClusterConnection) {
                Connect(connectionSettings.UserCredentials, connectionSettings.GossipSeeds, connectionSettings.VerboseLogging);
            }

            StartEventStore();
        }

        /// <summary>
        /// Connect to a single EventStore server with an IP address and a port
        /// </summary>
        /// <param name="credentials">UserCredentials: Username-Password used for authentication and authorization</param>
        /// <param name="tcpEndpoint">IPEndpoint: IP address and port of the EventStore server</param>
        /// <param name="verboseLogging">bool: Setup an EventStore connection with verbose logging turned on. Default = false.</param>
        private void Connect(
            UserCredentials credentials,
            IPEndPoint tcpEndpoint,
            bool verboseLogging = false) {

            var settings = SetClientConnectionSettings(credentials, verboseLogging);
            Connection = new EventStoreConnectionWrapper(EventStoreConnection.Create(settings, tcpEndpoint, $"{tcpEndpoint}-Single Connection"));
            if (Connection != null) return;
            _log.Error("EventStore Connection is null - Diagnostic Monitoring will be unavailable.");
            TeardownEventStore(false);
        }

        /// <summary>
        /// Connect to an EventStore server using DNS Discovery
        /// </summary>
        /// <remarks>
        /// Define a common DNS name relating it to all cluster node ID address(es).
        /// EventStore will process the DNS into gossip seeds for use in the connection.
        /// </remarks>
        /// <param name="credentials">UserCredentials: Username-Password used for authentication and authorization</param>
        /// <param name="clusterDns">string: Cluster DNS name representing all nodes in the EventStore cluster</param>
        /// <param name="networkPort">int: External HTTP port used for cluster gossip communication.</param>
        /// <param name="verboseLogging">bool: Setup an EventStore connection with verbose logging turned on. Default = false.</param>
        private void Connect(
            UserCredentials credentials,
            string clusterDns,
            int networkPort,
            bool verboseLogging = false) {

            var settings = SetClientConnectionSettings(credentials, verboseLogging);
            var esConn = EventStoreConnection.Create(settings,
                    ClusterSettings.Create()
                        .DiscoverClusterViaDns()
                        .SetClusterDns(clusterDns)
                        .SetClusterGossipPort(networkPort), 
                    $"{clusterDns}-Cluster Connection");

            Connection = new EventStoreConnectionWrapper(esConn);

            if (Connection != null) return;
            _log.Error("EventStore Connection is null - Diagnostic Monitoring will be unavailable.");
            TeardownEventStore(false);
        }

        /// <summary>
        /// Establish EventStore Cluster Connection via Gossip Seed IP address(es)
        /// </summary>
        /// <remarks>
        /// Connect to an EventStore cluster using gossip seed IP addresses.
        /// This supports both a single EventStore cluster node and a multi-node EventStore cluster.
        /// Note that a cluster of 1 is equivalent to a single instance.
        /// </remarks>
        /// <param name="credentials">UserCredentials: Username-Password used for authentication and authorization</param>
        /// <param name="gossipSeeds">Array of GossipSeeds: The TCP/IP addresses, port and header. Generated by the <see cref="StreamStoreConnectionSettings"/>.</param>
        /// <param name="verboseLogging">bool: Setup an EventStore connection with verbose logging turned on. Default = false.</param>
        private void Connect(
            UserCredentials credentials,
            GossipSeed[] gossipSeeds,
            bool verboseLogging = false) {

            var settings = SetClientConnectionSettings(credentials, verboseLogging);
            Connection = new EventStoreConnectionWrapper(
                EventStoreConnection.Create(settings,
                    ClusterSettings.Create()
                        .DiscoverClusterViaGossipSeeds()
                        .SetGossipSeedEndPoints(gossipSeeds),
                        "Gossip Seeds-Cluster Connection"));

            if (Connection != null) return;
            _log.Error($"EventStore Custer of {gossipSeeds.Length} Connection is null - Diagnostic Monitoring will be unavailable.");
            TeardownEventStore(false);
        }

        /// <summary>
        /// Connect to a single EventStore server with an IP address and a port
        /// </summary>
        /// <param name="credentials">UserCredentials: Username-Password used for authentication and authorization</param>
        /// <param name="server">IPAddress: IP address of the EventStore server</param>
        /// <param name="tcpPort">int: TCP communication port on the server</param>
        [Obsolete("Connect is deprecated, please use the EventStoreLoader constructor instead. Will be made removed in the next release.")]
        public void Connect(
                    UserCredentials credentials,
                    IPAddress server,
                    int tcpPort) {
        var tcpEndpoint = new IPEndPoint(server, tcpPort);

            var settings = ConnectionSettings.Create()
                .SetDefaultUserCredentials(new global::EventStore.ClientAPI.SystemData.UserCredentials(credentials.Username, credentials.Password))
                .KeepReconnecting()
                .KeepRetrying()
                .UseConsoleLogger()
                .Build();

            Connection = new EventStoreConnectionWrapper(EventStoreConnection.Create(settings, tcpEndpoint, "Default Connection"));
            if (Connection == null) {
                _log.Error("EventStore Connection is null - Diagnostic Monitoring will be unavailable.");
                TeardownEventStore(false);
                return;
            }

            StartEventStore();
        }

        public static EventData ToEventData(
            Guid eventId,
            object message,
            Dictionary<string, object> metaData) {
            dynamic typedMessage = message;
            var eventBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(typedMessage, SerializerSettings));
            var metaDataString = JsonConvert.SerializeObject(metaData, SerializerSettings);
            //Log.Debug("Metadata= " + metaDataString);
            var metaDataBytes = Encoding.UTF8.GetBytes(metaDataString);
            var typeName = typedMessage.GetType().Name;
            return new EventData(eventId, typeName, true, eventBytes, metaDataBytes);
        }

        /// <summary>
        /// Start the EventStore executable with the common default settings
        /// </summary>
        /// <param name="installPath">EventStore executable path</param>
        /// <param name="additionalArgs">Supplemental EventStore CLI arguments</param>
        public void SetupEventStore(DirectoryInfo installPath, string additionalArgs = null) {
            var args = $" --config=\"./config.yaml\" {additionalArgs ?? ""}";

            SetupEventStore(installPath,
                args,
                new UserCredentials("admin", "changeit"),
                IPAddress.Parse("127.0.0.1"),
                tcpPort: 1113,
                windowStyle: ProcessWindowStyle.Hidden,
                opt: StartConflictOption.Connect);
        }

        /// <summary>
        /// Start the EventStore executable explicit single server options, and then connect
        /// </summary>
        /// <param name="installPath">DirectoryInfo: EventStore executable path</param>
        /// <param name="args">string: EventStore CLI arguments</param>
        /// <param name="credentials">UserCredentials: Username-Password pair for authentication and authorization.</param>
        /// <param name="server">IPAddress: EventStore Server </param>
        /// <param name="tcpPort">int: Network port used for Tcp communication.</param>
        /// <param name="windowStyle">ProcessWindowStyle: How the EventStore executable window will be displayed or hidden.</param>
        /// <param name="opt">StartConflictOption Enum: What to do if a conflicting EventStore process is already running.</param>
        public void SetupEventStore(
                                DirectoryInfo installPath,
                                string args,
                                UserCredentials credentials,
                                IPAddress server,
                                int tcpPort,
                                ProcessWindowStyle windowStyle,
                                StartConflictOption opt) {
            Ensure.NotNullOrEmpty(args, "args");
            Ensure.NotNull(credentials, "credentials");

            var fullPath = Path.Combine(installPath.FullName, "EventStore.ClusterNode.exe");

            var runningEventStores = Process.GetProcessesByName("EventStore.ClusterNode");
            if (runningEventStores.Count() != 0) {
                switch (opt) {
                    case StartConflictOption.Connect:
                        _process = runningEventStores[0];
                        break;
                    case StartConflictOption.Kill:
                        foreach (var es in runningEventStores) {
                            es.Kill();
                        }
                        break;
                    case StartConflictOption.Error:
                        throw new Exception("Conflicting Eventstore running.");
                }
            }

            if (_process == null) {
                _process = new Process {
                    StartInfo = {
                        WindowStyle = windowStyle,
                        UseShellExecute = true,
                        CreateNoWindow = false,
                        WorkingDirectory = installPath.FullName,
                        FileName = fullPath,
                        Arguments = args,
                        Verb ="runas"
                    }
                };
                _process.Start();
            }
            Connect(credentials, new IPEndPoint(server,tcpPort));
        }

        /// <summary>
        /// Connect to EventStore and test the connection
        /// </summary>
        private void StartEventStore() {
            // ToDo: The connection settings to keep retrying in the EventStore code circumvents this loop of 8 tries never returning from the Connect call.
            Connection.Connect();
            const int retry = 8;
            var count = 0;
            do {
                try
                {
                    Connection.ReadStreamForward("by_event_type", 0, 1);
                    return;
                }
                catch { } //ignore
                Thread.Sleep(100);
                count++;
            } while (count < retry);

            throw new Exception("Unable to start EventStore");
        }

        /// <summary>
        /// Create EventStore ConnectionSettings
        /// </summary>
        /// <param name="credentials">ReactiveDomain Credentials</param>
        /// <param name="verboseLogging">True/False for </param>
        /// <returns>EventStore.ClientAPI.ConnectionSettings</returns>
        private static ConnectionSettings SetClientConnectionSettings(UserCredentials credentials, bool verboseLogging = false) {
            Ensure.NotNull(credentials, "credentials");

            if (verboseLogging) {
                return ConnectionSettings.Create()
                    .SetDefaultUserCredentials(
                        new global::EventStore.ClientAPI.SystemData.UserCredentials(credentials.Username, credentials.Password))
                    .KeepReconnecting()
                    .KeepRetrying()
                    .UseConsoleLogger()
                    .EnableVerboseLogging()
                    .Build();
            } else {
                return ConnectionSettings.Create()
                    .SetDefaultUserCredentials(
                        new global::EventStore.ClientAPI.SystemData.UserCredentials(credentials.Username, credentials.Password))
                    .KeepReconnecting()
                    .KeepRetrying()
                    .UseConsoleLogger()
                    .Build();
            }
        }

        /// <summary>
        ///  Terminate an EventStore instance created by StartEventStore
        /// </summary>
        /// <remarks>
        /// Yin for the <seealso="StartEventStore"/> yang.
        /// </remarks>
        /// <param name="leaveRunning">bool: true = close the connection, but leave the process running.</param>
        public void TeardownEventStore(bool leaveRunning = true) {
            Connection?.Close();
            if (leaveRunning || _process == null || _process.HasExited) return;
            _process.Kill();
            _process.WaitForExit();
        }

        /// <summary>
        /// Ensure the TeardownEventStore method is called
        /// </summary>
        /// <remarks>
        ///  <seealso cref="TeardownEventStore"/> is called with the leaveRunnig parameter = false
        /// </remarks>
        public void Dispose() {
            if (_disposed) return;
            TeardownEventStore();
            _disposed = true;
        }
    }
}