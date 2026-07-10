using System.Net;
using System.Net.Sockets;
using ReactiveDomain.Logging;
using ReactiveDomain.Util;

namespace ReactiveDomain.Transport;

public class TcpServerListener {
	private static readonly ILogger _log = LogManager.GetLogger("ReactiveDomain");

	private readonly EndPoint _serverEndPoint;
	private readonly Socket _listeningSocket;
	private readonly SocketArgsPool _acceptSocketArgsPool;
	private Action<IPEndPoint, Socket>? _onSocketAccepted;

	public TcpServerListener(EndPoint serverEndPoint) {
		Ensure.NotNull(serverEndPoint, "serverEndPoint");

		_serverEndPoint = serverEndPoint;

		_listeningSocket = new Socket(serverEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

		_acceptSocketArgsPool = new SocketArgsPool("TcpServerListener.AcceptSocketArgsPool",
			TcpConfiguration.ConcurrentAccepts * 2,
			CreateAcceptSocketArgs);
	}

	private SocketAsyncEventArgs CreateAcceptSocketArgs() {
		var socketArgs = new SocketAsyncEventArgs();
		socketArgs.Completed += AcceptCompleted;
		return socketArgs;
	}

	public void StartListening(Action<IPEndPoint, Socket> callback, string securityType) {
		Ensure.NotNull(callback, nameof(callback));

		_onSocketAccepted = callback;

		_log.Info("Starting {0} TCP listening on TCP endpoint: {1}.", securityType, _serverEndPoint);
		try {
			_listeningSocket.Bind(_serverEndPoint);
			_listeningSocket.Listen(TcpConfiguration.AcceptBacklogCount);
		} catch (Exception ex) {
			_log.InfoException(ex, "Failed to listen on TCP endpoint: {0}.", _serverEndPoint);
			Helper.EatException(() => _listeningSocket.Close(TcpConfiguration.SocketCloseTimeoutMs));
			throw;
		}

		for (int i = 0; i < TcpConfiguration.ConcurrentAccepts; ++i) {
			StartAccepting();
		}
	}

	private void StartAccepting() {
		var socketArgs = _acceptSocketArgsPool.Get();

		try {
			var firedAsync = _listeningSocket.AcceptAsync(socketArgs);
			if (!firedAsync)
				ProcessAccept(socketArgs);
		} catch (ObjectDisposedException) {
			HandleBadAccept(socketArgs);
		}
	}

	private void AcceptCompleted(object? sender, SocketAsyncEventArgs e) {
		ProcessAccept(e);
	}

	private void ProcessAccept(SocketAsyncEventArgs e) {
		if (e.SocketError != SocketError.Success) {
			HandleBadAccept(e);
		} else {
			var acceptSocket = e.AcceptSocket;
			e.AcceptSocket = null;
			_acceptSocketArgsPool.Return(e);

			if (acceptSocket is not null)
				OnSocketAccepted(acceptSocket);
		}

		StartAccepting();
	}

	private void HandleBadAccept(SocketAsyncEventArgs socketArgs) {
		Helper.EatException(() => socketArgs.AcceptSocket?.Close(TcpConfiguration.SocketCloseTimeoutMs));
		socketArgs.AcceptSocket = null;
		_acceptSocketArgsPool.Return(socketArgs);
	}

	private void OnSocketAccepted(Socket socket) {
		IPEndPoint socketEndPoint;
		try {
			socketEndPoint = (IPEndPoint)socket.RemoteEndPoint!;
		} catch (Exception) {
			return;
		}

		_onSocketAccepted?.Invoke(socketEndPoint, socket);
	}

	public void Stop() {
		Helper.EatException(() => _listeningSocket.Close(TcpConfiguration.SocketCloseTimeoutMs));
	}
}
