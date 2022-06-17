//#define LIVE_ES_CONNECTION

using System;
using ReactiveDomain.EventStore;
using ES = EventStore.ClientAPI;
using System.Net;


// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Testing
{
    public class StreamStoreConnectionFixture : IDisposable
    {
        private static readonly TimeSpan TimeToStop = TimeSpan.FromSeconds(5);

        private readonly IDisposable _node = null;

        public StreamStoreConnectionFixture()
        {
            AdminCredentials = new UserCredentials("admin", "changeit");
#if LIVE_ES_CONNECTION
            //Connection = new EventStoreConnectionWrapper(
            //                  EventStoreConnection.Create("ConnectTo=tcp://admin:changeit@localhost:1113; HeartBeatTimeout=500"));
            string esUser = "admin";
            string esPwd = "changeit";
            var creds = new ES.SystemData.UserCredentials(esUser, esPwd);
            string esIpAddress = "127.0.0.1";
            int esPort = 1113;          
            var tcpEndpoint = new IPEndPoint(IPAddress.Parse(esIpAddress), esPort);

            var settings = ES.ConnectionSettings.Create()
                .SetDefaultUserCredentials(creds)
                .KeepReconnecting()
                .KeepRetrying()
                .UseConsoleLogger()
                .DisableTls()
                .DisableServerCertificateValidation()
                .WithConnectionTimeoutOf(TimeSpan.FromSeconds(15))
                .Build();
            Connection = new EventStoreConnectionWrapper( ES.EventStoreConnection.Create(settings, tcpEndpoint, Guid.NewGuid().ToString()));
            Connection.Connect();           
#else

            Connection = new ReactiveDomain.Testing.EventStore.MockStreamStoreConnection("Test Fixture");
            Connection.Connect();
#endif
        }

        public IStreamStoreConnection Connection { get; }

        public UserCredentials AdminCredentials { get; }

        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            Connection?.Close();
            Connection?.Dispose();
            _node?.Dispose();
            _disposed = true;
        }
    }
}