using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using ReactiveDomain.Logging;
using ILogger = ReactiveDomain.Logging.ILogger;


namespace ReactiveDomain.EventStore
{
    public class EventStoreLauncher : IDisposable
    {
        /// <summary>
        /// Options when there is a process conflict when the EventStore process starts
        /// </summary>
        public enum StartConflictOption
        {
            Kill,
            Connect,
            Error
        }

        private readonly ILogger _log = LogManager.GetLogger("Common");
        private Process _process;
        private bool _disposed;
        private StartConflictOption _defaultStartOption;
        private ProcessWindowStyle _defaultWindowStyle;
        public EventStoreConnectionManager ESConnection { get; private set; }

        public EventStoreLauncher(
            StartConflictOption startConflictOption = StartConflictOption.Connect,
            ProcessWindowStyle windowStyle = ProcessWindowStyle.Hidden)
        {
            _defaultStartOption = startConflictOption;
            _defaultWindowStyle = windowStyle;
        }
        /// <summary>
        /// Start the EventStore executable explicit single server options, and then connect
        /// </summary>
        /// <param name="config">EsDb Config Section</param>
        public EventStoreConnectionManager SetupEventStore(EsdbConfig config)
        {
            return SetupEventStore(config,
                 windowStyle: _defaultWindowStyle,
                 opt: _defaultStartOption);
        }

        /// <summary>
        /// Start the EventStore executable explicit single server options, and then connect
        /// </summary>
        /// <param name="config">EsDb Config Section</param>
        /// <param name="windowStyle">ProcessWindowStyle: How the EventStore executable window will be displayed or hidden.</param>
        /// <param name="opt">StartConflictOption Enum: What to do if a conflicting EventStore process is already running.</param>
        public EventStoreConnectionManager SetupEventStore(
                        EsdbConfig config,
                        ProcessWindowStyle windowStyle,
                        StartConflictOption opt)
        {

            var fullPath = Path.Combine(config.Path, "EventStore.ClusterNode.exe");

            var runningEventStores = Process.GetProcessesByName("EventStore.ClusterNode");
            if (runningEventStores.Count() != 0)
            {
                switch (opt)
                {
                    case StartConflictOption.Connect:
                        _process = runningEventStores[0];
                        break;
                    case StartConflictOption.Kill:
                        foreach (var es in runningEventStores)
                        {
                            es.Kill();
                        }
                        break;
                    case StartConflictOption.Error:
                        throw new Exception("Conflicting EventStore running.");
                }
            }

            if (_process == null)
            {
                _process = new Process
                {
                    StartInfo = {
                        WindowStyle = windowStyle,
                        UseShellExecute = true,
                        CreateNoWindow = false,
                        WorkingDirectory = config.WorkingDir,
                        FileName = fullPath,
                        Arguments = config.Args,
                        Verb ="runas"
                    }
                };
                _process.Start();
            }

            ESConnection = new EventStoreConnectionManager(config);
            return ESConnection;
        }

        /// <summary>
        ///  Terminate an EventStore instance created by SetupEventStore
        /// </summary>
        /// <remarks>
        /// Yin for the <see cref="SetupEventStore"/> yang.
        /// </remarks>
        /// <param name="leaveRunning">bool: true = close the connection, but leave the process running.</param>
        public void TeardownEventStore(bool leaveRunning = true)
        {
            ESConnection?.Connection?.Close();
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
        public void Dispose()
        {
            if (_disposed) return;
            TeardownEventStore();
            _disposed = true;
        }
    }
}