using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Messaging.Tests;

public class RemoteBusFixture : IDisposable {
	public Dispatcher LocalBus { get; }
	public Dispatcher RemoteBus { get; }
	public long LocalMsgCount;
	public long RemoteMsgCount;
	public readonly TimeSpan StandardTimeout;
	private readonly BusConnector _connector;

	public RemoteBusFixture() {
		StandardTimeout = TimeSpan.FromMilliseconds(200);
		// These were passed as slowMsgThreshold/slowCmdThreshold back when those doubled as the ack and
		// response timeouts. They were always meant as timeouts, so they now say so.
		LocalBus = new Dispatcher(nameof(TestCommandBusFixture), 1, false,
			defaultAckTimeout: StandardTimeout, defaultResponseTimeout: StandardTimeout);
		RemoteBus = new Dispatcher(nameof(TestCommandBusFixture), 1, false,
			defaultAckTimeout: StandardTimeout, defaultResponseTimeout: StandardTimeout);

		LocalBus.SubscribeToAll(new AdHocHandler<IMessage>(_ => Interlocked.Increment(ref LocalMsgCount)));
		RemoteBus.SubscribeToAll(new AdHocHandler<IMessage>(_ => Interlocked.Increment(ref RemoteMsgCount)));

		_connector = new BusConnector(LocalBus, RemoteBus);

		Reset();
	}
	public void Reset() {
		Interlocked.Exchange(ref LocalMsgCount, 0);
		Interlocked.Exchange(ref RemoteMsgCount, 0);
	}
	protected virtual void Dispose(bool disposing) {
		if (disposing) {
			LocalBus.Dispose();
			RemoteBus.Dispose();
			_connector.Dispose();
		}
	}
	public void Dispose() {
		Dispose(true);
		GC.SuppressFinalize(this);
	}
}
