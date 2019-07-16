using EventStore.ClientAPI;
using System;
using System.Configuration;
using System.Net;

namespace Shovel
{
    using System.IO;
    using System.Reflection;
    using EventStore.ClientAPI.SystemData;

    public class Bootstrap
    {
        private IEventStoreConnection _sourceConnection;
        private IEventStoreConnection _targetConnection;
        private UserCredentials _sourceCredentials;
        private UserCredentials _targetCredentials;
        private Assembly _transformerAssembly;

        public bool Loaded { get; private set; }

        public void Load()
        {
            _sourceConnection = ConnectToEventStore(ReadSetting("sourceTcpAddress"), int.Parse(ReadSetting("sourcePort")));
            _targetConnection = ConnectToEventStore(ReadSetting("targetTcpAddress"), int.Parse(ReadSetting("targetPort")));
            _sourceCredentials = new UserCredentials(ReadSetting("sourceUsername"), ReadSetting("sourcePassword"));
            _targetCredentials = new UserCredentials(ReadSetting("targetUsername"), ReadSetting("targetPassword"));

            try
            {
                _transformerAssembly = Assembly.LoadFrom(@".\EventTransformer.dll");
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("No transformer assembly found.");
            }

            Loaded = true;
        }

        public void Run()
        {
            IEventTransformer transformer = null;

            if (_transformerAssembly != null)
            {
                var t = _transformerAssembly.GetType("EventTransformer.TransformerFactory");
                var methodInfoStatic = t.GetMethod("GetEventTransformer");
                if (methodInfoStatic == null)
                {
                    throw new AccessViolationException("No such static method exists.");
                }

                var o = Activator.CreateInstance(t, null);

                transformer = methodInfoStatic.Invoke(o, null) as IEventTransformer;
            }

            var processing = new EventShovel(_sourceConnection, _targetConnection, _sourceCredentials, _targetCredentials, transformer);
            processing.Run();
        }

        public static string ReadSetting(string key)
        {
            try
            {
                var appSettings = ConfigurationManager.AppSettings;
                string result = appSettings[key] ?? String.Empty;

                Console.WriteLine(result != String.Empty ? $"Setting {key}: {result}" : $"Setting {key} is not found");

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
