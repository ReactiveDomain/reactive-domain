using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Projections;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;
using ReactiveDomain.Util;
using ES_ILogger = EventStore.ClientAPI.ILogger;


namespace ReactiveDomain.EventStore {
    [Obsolete("Use EventStoreConnectionManager", false)]
    public class EventStoreLoader {
        public enum StartConflictOption {
            Kill,
            Connect,
            Error
        }

        private readonly Microsoft.Extensions.Logging.ILogger Log = Logging.LogProvider.GetLogger("Common");

        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None };
        private Process _process;
        public ProjectionsManager ProjectionsManager;

        public IStreamStoreConnection Connection { get; private set; }

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
            Connect(
                credentials,
                server,
                tcpPort);
        }
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
                //.EnableVerboseLogging()
                .Build();

            Connection = new EventStoreConnectionWrapper(EventStoreConnection.Create(settings, tcpEndpoint, "Default Connection"));

            if (Connection == null) {
                Log.LogError("EventStore Connection is null - Diagnostic Monitoring will be unavailable.");
                TeardownEventStore(false);
                return;
            }
            Connection.Connect();
            int retry = 8;
            int count = 0;
            do {
                try {
                    Connection.ReadStreamForward("by_event_type", 0, 1);
                    return;
                }
                catch {
                    //ignore
                }
                Thread.Sleep(100);
                count++;
            } while (count < retry);
            throw new Exception("Unable to start Eventstore");

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
        //
        //N.B. if we need this the use of deserialization via type id is unsafe and the full type name should be used instead
        //
        //public static void FromEventData(
        //    RecordedEvent recordedEvent,
        //    out dynamic message,
        //    out Dictionary<string, object> metaData)
        //{
        //    string metaDataString = Encoding.UTF8.GetString(recordedEvent.Metadata);
        //    metaData = JsonConvert.DeserializeObject<Dictionary<string, object>>(metaDataString, SerializerSettings);
        //    string eventString = Encoding.UTF8.GetString(recordedEvent.Data);
        //    if (metaData.ContainsKey("MsgTypeId"))
        //    {
        //        var msgTypeId = (long)metaData["MsgTypeId"];
        //        if (MessageHierarchy.MsgTypeByTypeId.ContainsKey((int)msgTypeId))
        //        {
        //            var msgType = MessageHierarchy.MsgTypeByTypeId[(int)msgTypeId];
        //            message = JsonConvert.DeserializeObject(eventString, msgType, SerializerSettings);
        //            return;
        //        }
        //    }
        //    message = JsonConvert.DeserializeObject(eventString, SerializerSettings);
        //    //Log.Warn("metaData did not contain MsgTypeId, and thus FromEventData() could not deserialize event as the correct object-type");
        //}

        public void TeardownEventStore(bool leaveRunning = true) {
            Connection?.Close();
            if (leaveRunning || _process == null || _process.HasExited) return;
            _process.Kill();
            _process.WaitForExit();
        }
    }
    public class NullLogger : ES_ILogger {
        public void Debug(string format, params object[] args) { }

        public void Debug(Exception ex, string format, params object[] args) { }

        public void Error(string format, params object[] args) { }

        public void Error(Exception ex, string format, params object[] args) { }

        public void Info(string format, params object[] args) { }

        public void Info(Exception ex, string format, params object[] args) { }
    }
}