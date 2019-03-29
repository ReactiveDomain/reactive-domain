using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using ReactiveDomain.Logging;
using ReactiveDomain.Util;
using ILogger = ReactiveDomain.Logging.ILogger;

namespace ReactiveDomain.EventStore {
    /// <summary>
    /// Used to build a <see cref="StreamStoreConnectionSettings"/> object to describe the connection to the
    /// underlying event based data store.
    /// </summary>
    public sealed class StreamStoreConnectionSettingsBuilder {

        private ILogger _log = new Logging.NullLogger();
        private bool _verboseLogging;
        private IPEndPoint _singleServerIpEndPoint;
        private string _clusterDns;
        private IPAddress[] _gossipSeeds;
        private int _gossipExternalHttpPort;
        private UserCredentials _defaultUserCredentials;
        
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
        /// Configures the connection to output log messages to the <see cref="Logging.ConsoleLogger" />.
        /// </summary>
        /// <returns></returns>
        public StreamStoreConnectionSettingsBuilder UseConsoleLogger() {
            _log = new ConsoleLogger();
            return this;
        }

        public StreamStoreConnectionSettingsBuilder UseLazyLogger(string loggerName) {
            Ensure.NotNull(loggerName, nameof(loggerName));
            _log = LogManager.GetLogger(loggerName);
            return this;
        }

        /// <summary>
        /// Sets the default Reactive Domain <see cref="UserCredentials"/> used for this connection.
        /// If user credentials are not given for an operation, these credentials will be used.
        /// </summary>
        /// <param name="userCredentials"></param>
        /// <returns></returns>
        public StreamStoreConnectionSettingsBuilder SetDefaultUserCredentials(UserCredentials userCredentials) {
            _defaultUserCredentials = userCredentials;
            return this;
        }

        /// <summary>
        /// Sets the single data store instance <see cref="IPEndPoint"/> using a TCP port.
        /// </summary>
        /// <param name="singleServerIpEndPoint">The IP and port combined in an TCP <see cref="IPEndPoint"/>.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">If <paramref name="singleServerIpEndPoint" /> is null or empty.</exception>
        public StreamStoreConnectionSettingsBuilder SetSingleServerIpEndPoint(IPEndPoint singleServerIpEndPoint) {
            Ensure.NotNull<IPEndPoint>(singleServerIpEndPoint, nameof(singleServerIpEndPoint));
            _singleServerIpEndPoint = singleServerIpEndPoint;
            return this;
        }

        /// <summary>
        /// Sets the single data store instance <see cref="IPEndPoint"/> using an IP address and TCP port.
        /// </summary>
        /// <param name="ipAddress">IPAddress: The IP address of the data store server.</param>
        /// <param name="tcpPort">int: The TCP port used to connect to the server.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">If <paramref name="ipAddress" /> or <paramref name="tcpPort"/> is null or empty.</exception>
        public StreamStoreConnectionSettingsBuilder SetSingleServerIpEndPoint(IPAddress ipAddress, int tcpPort) {
            Ensure.NotNull(ipAddress, nameof(ipAddress));
            Ensure.Between(1024, 65535, tcpPort, nameof(tcpPort));

            _singleServerIpEndPoint = new IPEndPoint(ipAddress, tcpPort);
            return this;
        }

        /// <summary>
        /// Sets the cluster DNS name.
        /// </summary>
        /// <param name="clusterDns">The DNS name under which cluster nodes are listed.</param>
        /// <returns>A <see cref="DnsClusterSettingsBuilder"/> for further configuration.</returns>
        /// <exception cref="ArgumentNullException">If <paramref name="clusterDns" /> is null or empty.</exception>
        public StreamStoreConnectionSettingsBuilder SetClusterDns(string clusterDns) {
            Ensure.NotNullOrEmpty(clusterDns, "clusterDns");
            _clusterDns = clusterDns;
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
            Ensure.Positive(clusterGossipPort, "clusterGossipPort");
            _gossipExternalHttpPort = clusterGossipPort;
            return this;
        }

        /// <summary>
        /// Turns on verbose <see cref="EventStoreConnection"/> internal logic logging. By contains default information about connection, disconnection and errors, but you can customize output.
        /// </summary>
        /// <returns></returns>
        public StreamStoreConnectionSettingsBuilder EnableVerboseLogging() {
            _verboseLogging = true;
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
        /// Sets the client to discover nodes using a DNS name and a well-known port.
        /// </summary>
        /// <returns>A <see cref="DnsClusterSettingsBuilder"/> for further configuration.</returns>
        public DnsClusterSettingsBuilder DiscoverClusterViaDns() {
            return new DnsClusterSettingsBuilder();
        }

        /// <summary>
        /// Convert the mutable <see cref="ConnectionSettingsBuilder"/> object to an immutable
        /// <see cref="StreamStoreConnectionSettings"/> object.
        /// </summary>
        public StreamStoreConnectionSettings Build() {
            return new StreamStoreConnectionSettings(
                _defaultUserCredentials,
                _singleServerIpEndPoint,
                _clusterDns,
                _gossipSeeds,
                _gossipExternalHttpPort,
                _log,
                _verboseLogging);
        }
    }
}
