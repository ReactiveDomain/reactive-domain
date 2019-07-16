using EventStore.ClientAPI;
using EventStore.ClientAPI.SystemData;
using System;
using System.Configuration;
using System.IO;
using System.Net;
using System.Reflection;

namespace Shovel
{
    public class Bootstrap
    {
        private EventShovelConfig _eventShovelConfig = new EventShovelConfig();
        private Assembly _transformerAssembly;

        public bool Loaded { get; private set; }

        public void Load()
        {
            _eventShovelConfig.SourceConnection =
                ConnectToEventStore(ReadSetting("sourceTcpAddress"), int.Parse(ReadSetting("sourcePort")));
            _eventShovelConfig.TargetConnection =
                ConnectToEventStore(ReadSetting("targetTcpAddress"), int.Parse(ReadSetting("targetPort")));
            _eventShovelConfig.SourceCredentials =
                new UserCredentials(ReadSetting("sourceUsername"), ReadSetting("sourcePassword"));
            _eventShovelConfig.TargetCredentials =
                new UserCredentials(ReadSetting("targetUsername"), ReadSetting("targetPassword"));

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
            if (_transformerAssembly != null)
            {
                var t = _transformerAssembly.GetType("EventTransformer.TransformerFactory");
                var methodInfoStatic = t.GetMethod("GetEventTransformer");
                if (methodInfoStatic == null)
                {
                    throw new AccessViolationException("No such static method exists.");
                }

                var o = Activator.CreateInstance(t, null);

                _eventShovelConfig.EventTransformer = methodInfoStatic.Invoke(o, null) as IEventTransformer;
            }

            var processing = new EventShovel(_eventShovelConfig);
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

        public void PrepareFilters(string[] args)
        {
            foreach (var argument in args)
            {
                var splitargument = argument.Split('=');
                switch (splitargument[0])
                {
                    case "stream":
                        if (splitargument[1].EndsWith("*"))
                        {
                            _eventShovelConfig.StreamWildcardFilter.Add(splitargument[1].Remove(splitargument[1].LastIndexOf("*")));
                        }
                        else
                        {
                            _eventShovelConfig.StreamFilter.Add(splitargument[1]);
                        }
                        break;
                    case "eventtype":
                        if (splitargument[1].EndsWith("*"))
                        {
                            _eventShovelConfig.EventTypeWildcardFilter.Add(splitargument[1].Remove(splitargument[1].LastIndexOf("*")));
                        }
                        else
                        {
                            _eventShovelConfig.EventTypeFilter.Add(splitargument[1]);
                        }
                        break;
                }
            }
        }
    }
}
