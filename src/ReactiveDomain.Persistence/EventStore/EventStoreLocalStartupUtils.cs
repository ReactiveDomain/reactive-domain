using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;
using ReactiveDomain.Util;


namespace ReactiveDomain.EventStore {
    public class EventStoreLocalStartupUtils {

        /// <summary>
        /// Options when there is a process conflict when the EventStore process starts
        /// </summary>
        public enum StartConflictOption {
            Kill,
            Connect,
            Error
        }

        private static readonly ILogger Log = Logging.LogProvider.GetLogger("Common");
        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None };
        private Process _process;
        private bool _disposed;

        public IStreamStoreConnection Connection { get; private set; }

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
                        throw new Exception("Conflicting EventStore running.");
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

            var sscSettings = StreamStoreConnectionSettings.Create()
                .SetUserCredentials(credentials)
                .SetSingleServerIpEndPoint(new IPEndPoint(server, tcpPort))
                .SetVerboseLogging(false);
            var eventStoreConnectionManager = new EventStoreConnectionManager(sscSettings);
            Connection = eventStoreConnectionManager.Connection;
        }

        /// <summary>
        ///  Terminate an EventStore instance created by SetupEventStore
        /// </summary>
        /// <remarks>
        /// Yin for the <see cref="SetupEventStore"/> yang.
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