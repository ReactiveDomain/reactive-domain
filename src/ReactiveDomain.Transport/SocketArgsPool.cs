using System.Collections.Concurrent;
using System.Net.Sockets;

namespace ReactiveDomain.Transport;

internal class SocketArgsPool {
	public readonly string Name;

	private readonly Func<SocketAsyncEventArgs> _socketArgsCreator;
	private readonly ConcurrentStack<SocketAsyncEventArgs> _socketArgsPool = new ConcurrentStack<SocketAsyncEventArgs>();

	public SocketArgsPool(string name, int initialCount, Func<SocketAsyncEventArgs> socketArgsCreator) {
		ArgumentOutOfRangeException.ThrowIfNegative(initialCount);

		Name = name;
		_socketArgsCreator = socketArgsCreator ?? throw new ArgumentNullException(nameof(socketArgsCreator));

		for (int i = 0; i < initialCount; ++i) {
			_socketArgsPool.Push(socketArgsCreator());
		}
	}

	public SocketAsyncEventArgs Get() {
		if (_socketArgsPool.TryPop(out var result))
			return result;
		return _socketArgsCreator();
	}

	public void Return(SocketAsyncEventArgs socketArgs) {
		_socketArgsPool.Push(socketArgs);
	}
}
