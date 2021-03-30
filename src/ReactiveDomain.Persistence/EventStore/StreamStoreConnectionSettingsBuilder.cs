using System;
using System.Net;
using EventStore.ClientAPI;

using ReactiveDomain.Util;

using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace ReactiveDomain.EventStore {
    /// <summary>
    /// Used to build a <see cref="StreamStoreConnectionSettings"/> object to describe the connection to the
    /// underlying event based data store.
    /// </summary>
    public class StreamStoreConnectionSettingsBuilder {
        private ILogger _log = Logging.LogProvider.GetLogger("ReactiveDomain.EventStore");
        private bool _verboseLogging;
        private IPEndPoint _singleServerIpEndPoint;
        private string _clusterDns;
        private IPEndPoint[] _ipEndPoints;
        private int _networkIpPort;

        private UserCredentials _userCredentials;
        private bool _useTlsConnection;
        private string _targetHost;
        private bool _validateServer;


        internal StreamStoreConnectionSettingsBuilder() { }

        /// <summary>
        /// Configures the connection to output log messages to the given <see cref="ILogger" />.
        /// You should implement this interface using another library such as NLog, seriLog or log4net.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger"/> to use.</param>
        /// <returns></returns>
        public StreamStoreConnectionSettingsBuilder UseCustomLogger(ILogger logger) {
            Ensure.NotNull(logger, nameof(logger));
            _log = logger;
            return this;
        }

        /// <summary>
        /// Sets the Reactive Domain <see cref="UserCredentials"/> used for this connection.
        /// </summary>
        /// <param name="userCredentials"></param>
        /// <returns></returns>
        public StreamStoreConnectionSettingsBuilder SetUserCredentials(UserCredentials userCredentials) {
            Ensure.NotNull(userCredentials, nameof(userCredentials));
            _userCredentials = userCredentials;
            return this;
        }

        /// <summary>
        /// Sets the single data store instance <see cref="IPEndPoint"/> using an IP address x.x.x.x and tcp port.
        /// </summary>
        /// <param name="ipEndPoint">IPEndPoint: The IP address and port used for the data store server.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">If <paramref name="ipEndPoint" /> is null or empty.</exception>
        public StreamStoreConnectionSettingsBuilder SetSingleServerIpEndPoint(IPEndPoint ipEndPoint) {
            Ensure.NotNull(ipEndPoint, nameof(ipEndPoint));
            _singleServerIpEndPoint = ipEndPoint;
            return this;
        }

        /// <summary>
        /// Sets the network port used to communicate with a single server EventStore.
        /// </summary>
        /// <remarks>
        /// For a single server connection, this is the TCP port (often 1113).
        /// </remarks>
        /// <param name="tcpPort">int: The network port used to connect to the server.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">If <paramref name="tcpPort"/> is not in the proper range.</exception>
        public StreamStoreConnectionSettingsBuilder SetSingleServerTcpPort(int tcpPort) {
            Ensure.Between(1024, 65535, tcpPort, nameof(tcpPort));
            _networkIpPort = tcpPort;
            return this;
        }

        /// <summary>
        /// Sets the cluster DNS name.
        /// </summary>
        /// <param name="clusterDns">The DNS name under which cluster nodes are listed.</param>
        /// <returns>A <see cref="StreamStoreConnectionSettingsBuilder"/> for further configuration.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="clusterDns" /> is null or empty.</exception>
        public StreamStoreConnectionSettingsBuilder SetClusterDns(string clusterDns) {
            Ensure.NotNullOrEmpty(clusterDns, "clusterDns");
            _clusterDns = clusterDns;
            return this;
        }

        /// <summary>
        /// Sets gossip seed IP Addresses for the client.
        /// </summary>
        /// <param name="ipEndPoints">IpEndPoint array: The IP addresses that will build the cluster <see cref="GossipSeed"/>s.</param>
        /// <returns>A <see cref="StreamStoreConnectionSettingsBuilder"/> for further configuration.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="ipEndPoints" /> is null or empty.</exception>
        public StreamStoreConnectionSettingsBuilder SetGossipSeedEndPoints(IPEndPoint[] ipEndPoints) {
            Ensure.NotNullOrEmpty(ipEndPoints, nameof(ipEndPoints));
            _ipEndPoints = new IPEndPoint[ipEndPoints.Length];
            Array.Copy(ipEndPoints, _ipEndPoints, ipEndPoints.Length);
            return this;
        }

        /// <summary>
        /// Sets the well-known port on which the cluster gossip is taking place. This is used by DNS and Gossip Seed connections.
        /// </summary>
        /// <remarks>
        /// For EventStore with Manager nodes (ie the commercial edition) this
        /// should be the port number of the External HTTP port on which the  managers are running.
        /// 
        /// The open source edition uses the External HTTP port that the nodes are running on.
        ///
        /// If you cannot use a well-known port for this across all nodes, you can instead use gossip
        /// seed discovery and set the <see cref="IPEndPoint" /> of some seed nodes instead.
        /// </remarks>
        /// <param name="clusterGossipPort">The cluster gossip port.</param>
        /// <returns></returns>
        public StreamStoreConnectionSettingsBuilder SetClusterGossipPort(int clusterGossipPort) {
            Ensure.Between(1024, 65535, clusterGossipPort, nameof(clusterGossipPort));
            _networkIpPort = clusterGossipPort;
            return this;
        }

        /// <summary>
        /// Turns on verbose <see cref="EventStoreConnection"/> internal logic logging. 
        /// </summary>
        /// <remarks>
        /// Contains information about connection, disconnection and errors.
        /// </remarks>
        /// <param name="verbose">Turn verbose logging on or off.</param>
        /// <returns></returns>
        public StreamStoreConnectionSettingsBuilder SetVerboseLogging(bool verbose) {
            _verboseLogging = verbose;
            return this;
        }

        /// <summary>
        /// Uses a TLS connection over TCP. This should generally be used with authentication.
        /// </summary>
        /// <remarks>
        /// Exposing the underlying EventStore setting to use a TLS connection. Taken from <see cref="ConnectionSettingsBuilder"/>.
        /// If this method is not used, then TLS is not used. <see cref="useTls"/> provided to allow TLS to be optional without changing code.
        /// </remarks>
        /// <param name="useTls">bool: Use a secure encrypted TLS connection to EventStore.</param>
        /// <param name="targetHost">string: HostName of server certificate. Optional, unless <see cref="useTls"/> is true.</param>
        /// <param name="validateServer">bool: Accept connection from server with untrusted certificate (optional, defaults to false).</param>
        /// <returns></returns>
        public StreamStoreConnectionSettingsBuilder SetTlsConnection(bool useTls,
            string targetHost = "", 
            bool validateServer = false) {

            if (useTls)
                Ensure.NotNullOrEmpty(targetHost, "targetHost");
            _useTlsConnection = useTls;
            _targetHost = targetHost;
            _validateServer = validateServer;
            return this;
        }

        /// <summary>
        /// Convert the mutable <see cref="StreamStoreConnectionSettingsBuilder"/> object to an immutable
        /// <see cref="StreamStoreConnectionSettings"/> object.
        /// </summary>
        /// <param name="builder">The <see cref="StreamStoreConnectionSettingsBuilder"/> to convert.</param>
        /// <returns>An immutable <see cref="ConnectionSettings"/> object with the values specified by the builder.</returns>
        public static implicit operator StreamStoreConnectionSettings(StreamStoreConnectionSettingsBuilder builder) {
            return builder.Build();
        }

        /// <summary>
        /// Convert the mutable <see cref="ConnectionSettingsBuilder"/> object to an immutable
        /// <see cref="StreamStoreConnectionSettings"/> object.
        /// </summary>
        public StreamStoreConnectionSettings Build() {
            return new StreamStoreConnectionSettings(
                _userCredentials,
                _singleServerIpEndPoint,
                _clusterDns,
                _ipEndPoints,
                _networkIpPort,
                _log,
                _useTlsConnection,
                _targetHost,
                _validateServer,
                _verboseLogging);
        }
    }
}
