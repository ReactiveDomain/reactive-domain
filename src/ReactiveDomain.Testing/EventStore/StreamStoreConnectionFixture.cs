//#define LIVE_ES_CONNECTION

// References needed if the #define above is uncommented
using System.Net;
using ReactiveDomain.EventStore;
using ES = EventStore.ClientAPI;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Testing;

public sealed class StreamStoreConnectionFixture : IDisposable {
	private readonly IDisposable? _node = null;

	public StreamStoreConnectionFixture() {
		AdminCredentials = new UserCredentials("admin", "changeit");
#if LIVE_ES_CONNECTION
		//Connection = new EventStoreConnectionWrapper(
		//                  EventStoreConnection.Create("ConnectTo=tcp://admin:changeit@localhost:1113; HeartBeatTimeout=500"));
		const string esUser = "admin";
		const string esPwd = "changeit";
		var creds = new ES.SystemData.UserCredentials(esUser, esPwd);
		const string esIpAddress = "127.0.0.1";
		const int esPort = 1113;
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
		Connection = new EventStoreConnectionWrapper(ES.EventStoreConnection.Create(settings, tcpEndpoint, Guid.NewGuid().ToString()));
		Connection.Connect();
#else
		Connection = new EventStore.MockStreamStoreConnection("Test Fixture");
		Connection.Connect();
#endif
	}

	public IStreamStoreConnection Connection { get; }

	public UserCredentials AdminCredentials { get; }

	private bool _disposed;
	public void Dispose() {
		if (_disposed)
			return;
		Connection.Close();
		Connection.Dispose();
		_node?.Dispose();
		_disposed = true;
	}
}
