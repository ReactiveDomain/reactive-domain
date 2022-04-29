using System;
using System.Threading;
using EventStore.ClientAPI;
using Newtonsoft.Json;
using ReactiveDomain.Logging;
using ILogger = ReactiveDomain.Logging.ILogger;


namespace ReactiveDomain.EventStore
{
    public class EventStoreConnectionManager
    {

        private readonly ILogger _log = LogManager.GetLogger(nameof(EventStoreConnectionManager));
        private static readonly JsonSerializerSettings SerializerSettings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None };

        public IStreamStoreConnection Connection => ESConnection;
        public EventStoreConnectionWrapper ESConnection { get; private set; }

        /// <summary>
        /// Setup EventStore based on the config settings provided by the caller
        /// </summary>
        /// <param name="config"><see cref="EsdbConfig"/> defined by the caller.</param>
        public EventStoreConnectionManager(EsdbConfig config)
        {
            ESConnection = new EventStoreConnectionWrapper(EventStoreConnection.Create(config.ConnectionString));

            //TODO: The connection settings to keep retrying in the EventStore code circumvents this loop of 8 tries never returning from the Connect call.
            Connection.Connect();
            const int retry = 8;
            var count = 0;
            do
            {
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
    }
}