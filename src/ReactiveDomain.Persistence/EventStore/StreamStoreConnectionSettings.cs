using System.Collections.Generic;
using System.Linq;
using System.Net;
using EventStore.ClientAPI;
using ReactiveDomain.Util;

namespace ReactiveDomain.EventStore
{
    /// <summary>
    /// A <see cref="StreamStoreConnectionSettings"/> object is an immutable representation of the settings for an
    /// <see cref="IStreamStoreConnection"/>.
    /// </summary>
    /// <remarks>
    /// The preferred method to build a <see cref="StreamStoreConnectionSettings"/> object is using
    /// a <see cref="StreamStoreConnectionSettingsBuilder"/>, either via the <see cref="Create"/> method, or via
    /// the constructor of <see cref="StreamStoreConnectionSettingsBuilder"/>.
    /// This class supports connections to a single node or a cluster of nodes. There is no preference of the connection
    /// type. If conflicting values are provided, a <see cref="StreamStoreConnectionException"/> is thrown.
    /// </remarks>
    public sealed class StreamStoreConnectionSettings
    {
        /// <summary>
        /// Creates a new set of <see cref="ConnectionSettings"/>.
        /// </summary>
        /// <returns>
        /// A <see cref="ConnectionSettingsBuilder"/> you can use to build up a <see cref="ConnectionSettings"/> via
        /// the Fluent design pattern.
        /// </returns>.
        public static StreamStoreConnectionSettingsBuilder Create() {
            return new StreamStoreConnectionSettingsBuilder();
        }

        /// <summary>
        /// The <see cref="ReactiveDomain.UserCredentials"/> to use for operations.
        /// </summary>
        public readonly UserCredentials UserCredentials;

        /// <summary>
        /// Whether the connection is encrypted using TLS.
        /// </summary>
        public readonly bool UseTlsConnection;

        /// <summary>
        /// The host name of the server expected on the TLS certificate.
        /// </summary>
        public readonly string TargetHost;

        /// <summary>
        /// Whether to validate the server TLS certificate.
        /// </summary>
        public readonly bool ValidateServer;

        /// <summary>
        /// The DNS name to use for discovering endpoints.
        /// </summary>
        public readonly string ClusterDns;

        /// <summary>
        /// Endpoints for seeding gossip if not using DNS.
        /// </summary>
        public readonly GossipSeed[] GossipSeeds;

        /// <summary>
        /// Network IP Port used by the connection. Can be the single server TCP port, or the external HTTP port used for cluster gossip communication.
        /// </summary>
        public readonly int NetworkIpPort;

        /// <summary>
        /// The IPAddress of a single data store instance.
        /// </summary>
        public readonly IPEndPoint SingleServerIpEndPoint;

        /// <summary>
        /// Apply the verbose internal logging from <see cref="EventStoreConnection"/> internal logic. Default: False.
        /// </summary>
        public readonly bool VerboseLogging;

        /// <summary>
        /// The <see cref="Microsoft.Extensions.Logging.ILogger"/> that this connection will use.
        /// </summary>
        public readonly Microsoft.Extensions.Logging.ILogger Log;

        /// <summary>
        /// Convenience connection type variables
        /// </summary>
        private ConnectionType _connectionType = ConnectionType.NoConnection;

        private enum ConnectionType {
            NoConnection,
            SingleNode,
            DnsCluster,
            GossipSeedsCluster
        }

        public bool IsSingleConnection => _connectionType == ConnectionType.SingleNode;
        public bool IsDnsClusterConnection => _connectionType == ConnectionType.DnsCluster;
        public bool IsGossipSeedClusterConnection => _connectionType == ConnectionType.GossipSeedsCluster;

        internal StreamStoreConnectionSettings(
            UserCredentials userCredentials,
            IPEndPoint singleServerIpEndPoint,
            string clusterDns,
            IReadOnlyList<IPEndPoint> ipEndPoints,
            int networkIpPort,
            Microsoft.Extensions.Logging.ILogger log,
            bool useTlsConnection,
            string targetHost,
            bool validateServer,
            bool verboseLogging = false)
        {
            Ensure.NotNull(log, nameof(log));
            Ensure.NotNull(userCredentials, nameof(userCredentials));
            if (useTlsConnection)
            {
                Ensure.NotNullOrEmpty(targetHost, nameof(targetHost));
            }

            if (singleServerIpEndPoint != null && !string.IsNullOrWhiteSpace(clusterDns) ||
                singleServerIpEndPoint != null && ipEndPoints != null && ipEndPoints.Count > 0 ||
                !string.IsNullOrWhiteSpace(clusterDns) && ipEndPoints != null && ipEndPoints.Count > 0) {
                    throw new StreamStoreConnectionException("Conflicting server or cluster input passed.");
            }

            if (singleServerIpEndPoint != null) {
                Ensure.Between(1024, 65535, singleServerIpEndPoint.Port, nameof(singleServerIpEndPoint.Port));
                _connectionType = ConnectionType.SingleNode;
            } else if (!string.IsNullOrWhiteSpace(clusterDns)) {
                Ensure.Between(1024, 65535, networkIpPort, nameof(networkIpPort));
                _connectionType = ConnectionType.DnsCluster;
            } else if (ipEndPoints != null && ipEndPoints.Count > 0) {
                foreach (var endPoint in ipEndPoints) {
                    Ensure.Between(1024, 65535, endPoint.Port, nameof(endPoint.Port));
                }
                _connectionType = ConnectionType.GossipSeedsCluster;
                GossipSeeds = ipEndPoints.Select(x => new GossipSeed(x)).ToArray();
            }

            UserCredentials = userCredentials;
            SingleServerIpEndPoint = singleServerIpEndPoint;
            ClusterDns = clusterDns;
            NetworkIpPort = networkIpPort;
            Log = log;
            UseTlsConnection = useTlsConnection;
            ValidateServer = validateServer;
            TargetHost = targetHost;
            VerboseLogging = verboseLogging;
        }
    }
}