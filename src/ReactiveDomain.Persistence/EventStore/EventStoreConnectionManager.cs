using System;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;

using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using ReactiveDomain.Util;


namespace ReactiveDomain.EventStore {
    public class EventStoreConnectionManager {
        private readonly Microsoft.Extensions.Logging.ILogger Log = Logging.LogProvider.GetLogger<EventStoreConnectionManager>();
        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None };

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
        public EventStoreConnectionManager(StreamStoreConnectionSettings connectionSettings)
        {
            if (connectionSettings.IsSingleConnection) {
                Connect(connectionSettings.UserCredentials, 
                    connectionSettings.SingleServerIpEndPoint, 
                    connectionSettings.UseTlsConnection, 
                    connectionSettings.TargetHost, 
                    connectionSettings.ValidateServer, 
                    connectionSettings.VerboseLogging);
            } else if (connectionSettings.IsDnsClusterConnection) {
                Connect(connectionSettings.UserCredentials,
                    connectionSettings.ClusterDns,
                    connectionSettings.NetworkIpPort,
                    connectionSettings.UseTlsConnection,
                    connectionSettings.TargetHost,
                    connectionSettings.ValidateServer,
                    connectionSettings.VerboseLogging);
            } else if (connectionSettings.IsGossipSeedClusterConnection) {
                Connect(connectionSettings.UserCredentials, 
                    connectionSettings.GossipSeeds,
                    connectionSettings.UseTlsConnection,
                    connectionSettings.TargetHost,
                    connectionSettings.ValidateServer,
                    connectionSettings.VerboseLogging);
            } else {
                throw new EventStoreConnectionException(
                    "EventStoreConnectionManager invalid settings. Minimum values: UserCredentials, SingleServerIpAddress, ClusterDns, or GossipSeeds required.");
            }

            StartEventStore();
        }

        /// <summary>
        /// Connect to a single EventStore server with an IP address and a port
        /// </summary>
        /// <param name="credentials">UserCredentials: Username-Password used for authentication and authorization</param>
        /// <param name="serverIpEndPoint"><see cref="IPEndPoint"/>: IP address and port of the EventStore server</param>
        /// <param name="useTlsConnection">bool: Use an encrypted TLS connection to EventStore server. (optional, defaults to false.)</param>
        /// <param name="tlsCertificateHostName">string: The host name of the server expected on the TLS certificate. (optional unless <see cref="useTlsConnection"/> is true.)</param>
        /// <param name="validateTlsCertificate">bool: Validate the server TLS certificate. (optional, defaults to false. Used if <see cref="useTlsConnection"/> is true.)</param>
        /// <param name="verboseLogging">bool: Verbose Logging True/False (optional, defaults to false)</param>
        private void Connect(
            UserCredentials credentials,
            IPEndPoint serverIpEndPoint,
            bool useTlsConnection = false,
            string tlsCertificateHostName = "",
            bool validateTlsCertificate = false,
            bool verboseLogging = false) {

            var settings = SetClientConnectionSettings(
                    credentials,
                    useTlsConnection,
                    tlsCertificateHostName,
                    validateTlsCertificate,
                    verboseLogging);

            Connection = new EventStoreConnectionWrapper(
                             EventStoreConnection.Create(settings, serverIpEndPoint, $"{serverIpEndPoint}-Single Connection")
            );
            if (Connection != null) return;
            Log.LogError("Connection to EventStore is null,  - Diagnostic Monitoring will be unavailable.");
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
        /// <param name="useTlsConnection">bool: Use an encrypted TLS connection to EventStore server. (optional, defaults to false.)</param>
        /// <param name="tlsCertificateHostName">string: The host name of the server expected on the TLS certificate. (optional unless <see cref="useTlsConnection"/> is true.)</param>
        /// <param name="validateTlsCertificate">bool: Validate the server TLS certificate. (optional, defaults to false. Used if <see cref="useTlsConnection"/> is true.)</param>
        /// <param name="verboseLogging">bool: Verbose Logging True/False (optional, defaults to false)</param>
        private void Connect(
            UserCredentials credentials,
            string clusterDns,
            int networkPort,
            bool useTlsConnection = false,
            string tlsCertificateHostName = "",
            bool validateTlsCertificate = false,
            bool verboseLogging = false) {

            var settings = SetClientConnectionSettings(
                    credentials,
                    useTlsConnection,
                    tlsCertificateHostName,
                    validateTlsCertificate,
                    verboseLogging);

            var esConn = EventStoreConnection.Create(settings,
                    ClusterSettings.Create()
                        .DiscoverClusterViaDns()
                        .SetClusterDns(clusterDns)
                        .SetClusterGossipPort(networkPort), 
                    $"{clusterDns}-Cluster Connection");

            Connection = new EventStoreConnectionWrapper(esConn);

            if (Connection != null) return;
            Log.LogError("EventStore Connection is null - Diagnostic Monitoring will be unavailable.");
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
        /// <param name="useTlsConnection">bool: Use an encrypted TLS connection to EventStore server. (optional, defaults to false.)</param>
        /// <param name="tlsCertificateHostName">string: The host name of the server expected on the TLS certificate. (optional unless <see cref="useTlsConnection"/> is true.)</param>
        /// <param name="validateTlsCertificate">bool: Validate the server TLS certificate. (optional, defaults to false. Used if <see cref="useTlsConnection"/> is true.)</param>
        /// <param name="verboseLogging">bool: Verbose Logging True/False (optional, defaults to false)</param>
        private void Connect(
            UserCredentials credentials,
            GossipSeed[] gossipSeeds,
            bool useTlsConnection = false,
            string tlsCertificateHostName = "",
            bool validateTlsCertificate = false,
            bool verboseLogging = false) {

            var settings = SetClientConnectionSettings(
                credentials,
                useTlsConnection,
                tlsCertificateHostName,
                validateTlsCertificate,
                verboseLogging);

            Connection = new EventStoreConnectionWrapper(
                EventStoreConnection.Create(settings,
                    ClusterSettings.Create()
                        .DiscoverClusterViaGossipSeeds()
                        .SetGossipSeedEndPoints(gossipSeeds),
                        "Gossip Seeds-Cluster Connection"));

            if (Connection != null) return;
            Log.LogError($"EventStore Custer of {gossipSeeds.Length} Connection is null - Diagnostic Monitoring will be unavailable.");
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
                try {
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
        /// <param name="credentials">Reactive Domain Credentials. (required)</param>
        /// <param name="useTlsConnection">bool: Use an encrypted TLS connection to EventStore server. (optional, defaults to false.)</param>
        /// <param name="tlsCertificateHostName">string: The host name of the server expected on the TLS certificate. (optional unless <see cref="useTlsConnection"/> is true.)</param>
        /// <param name="validateTlsCertificate">bool: Validate the server TLS certificate. (optional, defaults to false. Used if <see cref="useTlsConnection"/> is true.)</param>
        /// <param name="verboseLogging">bool: Verbose Logging True/False (optional, defaults to false)</param>
        /// <returns>EventStore.ClientAPI.ConnectionSettings</returns>
        private static ConnectionSettings SetClientConnectionSettings(UserCredentials credentials,
            bool useTlsConnection = false,
            string tlsCertificateHostName = "",
            bool validateTlsCertificate = false,
            bool verboseLogging = false)
        {
            Ensure.NotNull(credentials, "credentials");

            return ConnectionSettings.Create()
                .SetDefaultUserCredentials(credentials.ToESCredentials())
                .KeepReconnecting()
                .KeepRetrying()
                .UseConsoleLogger()
                .If(() => useTlsConnection, (x) => x.UseSslConnection(tlsCertificateHostName, validateTlsCertificate))
                .If(() => verboseLogging, (x) => x.EnableVerboseLogging())
                .Build();
        }
    }
}