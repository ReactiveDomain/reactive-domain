using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Transport.Tests;

[Collection("TCP bus tests")]
public class TcpBusServerSideTests {
	private readonly IPAddress _hostAddress = IPAddress.Loopback;
	private readonly TaskCompletionSource<IMessage> _tcs = new();
	private readonly CancellationTokenSource _cts = new(1000);

	public TcpBusServerSideTests() {
		_cts.Token.Register(() => _tcs.TrySetCanceled(), false);
	}

	private static int GetFreePort() {
		var listener = new TcpListener(IPAddress.Loopback, 0);
		listener.Start();
		var port = ((IPEndPoint)listener.LocalEndpoint).Port;
		listener.Stop();
		return port;
	}

	[Fact]
	public async Task can_handle_split_frames() {
		// 16kb large enough to cause the transport to split up the frame.
		// it would be better if we did the splitting manually so we were sure it really happened.
		// would require mocking more things.
		var prop1 = "prop1";
		var prop2 = string.Join("", Enumerable.Repeat("a", 16 * 1024));

		// server side
		var serverInbound = new QueuedHandler(
			new AdHocHandler<IMessage>(_tcs.SetResult),
			"InboundMessageQueuedHandler",
			true,
			TimeSpan.FromMilliseconds(1000));

		var port = GetFreePort();

		using var tcpBusServerSide = new TcpBusServerSide(
			_hostAddress,
			port,
			inboundNondiscardingMessageTypes: [typeof(WoftamEvent)],
			inboundNondiscardingMessageQueuedHandler: serverInbound);

		serverInbound.Start();

		// client side
		using var tcpBusClientSide = new TcpBusClientSide(_hostAddress, port);

		// wait for tcp connection to be established
		AssertEx.IsOrBecomesTrue(() => tcpBusClientSide.IsConnected, 200);

		// put message into client
		tcpBusClientSide.Handle(new WoftamEvent(prop1, prop2));

		// expect to receive it in the server
		var message = await _tcs.Task;
		Assert.NotNull(message);
		var evt = Assert.IsType<WoftamEvent>(message);
		Assert.Equal(prop1, evt.Property1);
		Assert.Equal(prop2, evt.Property2);
	}

	[Fact]
	public async Task can_filter_out_message_types() {
		// server side
		var serverInbound = new QueuedHandler(
			new AdHocHandler<IMessage>(_tcs.SetResult),
			"InboundMessageQueuedHandler",
			true,
			TimeSpan.FromMilliseconds(1000));

		var port = GetFreePort();

		using var tcpBusServerSide = new TcpBusServerSide(
			_hostAddress,
			port,
			inboundNondiscardingMessageTypes: [typeof(WoftamEvent)],
			inboundNondiscardingMessageQueuedHandler: serverInbound);

		serverInbound.Start();

		// client side
		using var tcpBusClientSide = new TcpBusClientSide(_hostAddress, port);

		// wait for tcp connection to be established
		AssertEx.IsOrBecomesTrue(() => tcpBusClientSide.IsConnected, 200);

		// put disallowed message into client
		tcpBusClientSide.Handle(new WoftamCommand("abc"));

		// expect to receive it in the server but drop it on the floor
		await Assert.ThrowsAsync<TaskCanceledException>(async () => await _tcs.Task);
	}
}
