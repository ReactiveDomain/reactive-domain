using EventStore.ClientAPI;
using System;
using System.Configuration;
using System.Net;

namespace Shovel
{
    public class Bootstrap
    {
        private IEventStoreConnection _sourceConnection;
        private IEventStoreConnection _targetConnection;

        public bool Loaded { get; private set; }

        public void Load()
        {
            _sourceConnection = ConnectToEventStore(ReadSetting("sourceTcpAddress"), int.Parse(ReadSetting("sourcePort")));
            _targetConnection = ConnectToEventStore(ReadSetting("targetTcpAddress"), int.Parse(ReadSetting("targetPort")));

            Loaded = true;
        }

        public void Run()
        {

        }

        private static string ReadSetting(string key)
        {
            try
            {
                var appSettings = ConfigurationManager.AppSettings;
                string result = appSettings[key] ?? String.Empty;

                Console.WriteLine(result != String.Empty ? result : $"Setting {key} is not found");

                return result;
            }
            catch (ConfigurationErrorsException)
            {
                Console.WriteLine("Error reading app settings");
                return String.Empty;
            }
        }

        private static IEventStoreConnection ConnectToEventStore(string ipAddress, int port)
        {
            var tcp = new IPEndPoint(IPAddress.Parse(ipAddress), port);
            var connection = EventStoreConnection.Create(tcp);
            connection.ConnectAsync().Wait();
            return connection;
        }
    }
}
