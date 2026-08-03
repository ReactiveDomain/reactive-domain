using System.Reactive;
using ReactiveDomain.Util;

namespace ReactiveDomain.Messaging.Bus;

/// <inheritdoc cref="IDispatcher"/>
public class Dispatcher : IDispatcher {
	private readonly Dictionary<Type, object> _handleWrappers;
	private readonly MultiQueuedPublisher _queuedPublisher;
	private readonly InMemoryBus _bus;
	private bool _disposed;
	public bool Idle => _queuedPublisher.Idle;

	/// <inheritdoc cref="CommandManager.AckTimeout"/>
	public TimeSpan AckTimeout {
		get => _queuedPublisher.AckTimeout;
		set => _queuedPublisher.AckTimeout = value;
	}

	/// <inheritdoc cref="CommandManager.ResponseTimeout"/>
	public TimeSpan ResponseTimeout {
		get => _queuedPublisher.ResponseTimeout;
		set => _queuedPublisher.ResponseTimeout = value;
	}

	/// <inheritdoc cref="CommandManager.SlowCommandThreshold"/>
	public TimeSpan SlowCommandThreshold {
		get => _queuedPublisher.SlowCommandThreshold;
		set => _queuedPublisher.SlowCommandThreshold = value;
	}
	/// <summary>
	/// Creates a dispatcher over a bus named <paramref name="name"/>, sending commands with the default
	/// timeouts supplied here.
	/// </summary>
	/// <param name="name">The name of the underlying bus.</param>
	/// <param name="queueCount">The number of publish queues; zero publishes on the calling thread.</param>
	/// <param name="watchSlowMsg">Log messages that take longer than <paramref name="slowMsgThreshold"/>.</param>
	/// <param name="slowMsgThreshold">Diagnostic only: a message that takes longer than this to handle is
	/// logged as slow. It is not a timeout and has no bearing on when a command fails — set
	/// <paramref name="defaultAckTimeout"/> for that.</param>
	/// <param name="slowCmdThreshold">Diagnostic only: a command whose round trip exceeds this is logged
	/// as slow. It is not a timeout and has no bearing on when a command fails — set
	/// <paramref name="defaultResponseTimeout"/> for that.</param>
	/// <param name="defaultAckTimeout">The ack timeout for every command this dispatcher sends without
	/// an explicit one. Resolution is per-send, then this, then
	/// <see cref="CommandManager.DefaultAckTimeout"/>.</param>
	/// <param name="defaultResponseTimeout">The response timeout for every command this dispatcher sends
	/// without an explicit one — including sends nested inside a handler, which have no send site to pass
	/// a timeout from. Resolution is per-send, then this, then
	/// <see cref="CommandManager.DefaultResponseTimeout"/>.</param>
	public Dispatcher(
		string name,
		uint queueCount = 0,
		bool watchSlowMsg = false,
		TimeSpan? slowMsgThreshold = null,
		TimeSpan? slowCmdThreshold = null,
		TimeSpan? defaultAckTimeout = null,
		TimeSpan? defaultResponseTimeout = null) {
		_bus = new InMemoryBus(name, watchSlowMsg, slowMsgThreshold);
		_queuedPublisher = new MultiQueuedPublisher(
			_bus, queueCount, slowMsgThreshold, slowCmdThreshold, defaultAckTimeout, defaultResponseTimeout);
		_handleWrappers = new Dictionary<Type, object>();
	}


	/// <summary>
	/// Enqueue a command and block until completed
	/// </summary>
	/// <param name="command"></param>
	/// <param name="exceptionMsg"></param>
	/// <param name="responseTimeout"></param>
	/// <param name="ackTimeout"></param>
	/// <returns></returns>
	public void Send(
		ICommand command,
		string? exceptionMsg = null,
		TimeSpan? responseTimeout = null,
		TimeSpan? ackTimeout = null)
		=> _queuedPublisher.Send(command, exceptionMsg, responseTimeout, ackTimeout);

	/// <summary>
	///  Enqueue a command and block until completed
	/// </summary>
	/// <param name="command"></param>
	/// <param name="response"></param>
	/// <param name="responseTimeout"></param>
	/// <param name="ackTimeout"></param>
	/// <returns>Command returned success</returns>
	public bool TrySend(
		ICommand command,
		out CommandResponse response,
		TimeSpan? responseTimeout = null,
		TimeSpan? ackTimeout = null)
		=> _queuedPublisher.TrySend(command, out response, responseTimeout, ackTimeout);

	/// <summary>
	/// Enqueue a command and return
	/// </summary>
	/// <param name="command"></param>
	/// <param name="responseTimeout"></param>
	/// <param name="ackTimeout"></param>
	/// <returns>Command enqueued</returns>
	public bool TrySendAsync(
		ICommand command,
		TimeSpan? responseTimeout = null,
		TimeSpan? ackTimeout = null)
		=> _queuedPublisher.TrySendAsync(command, responseTimeout, ackTimeout);

	public IDisposable Subscribe<T>(IHandleCommand<T> handler) where T : class, ICommand {
		if (HasSubscriberFor<T>())
			throw new ExistingHandlerException("Duplicate registration for command type.");
		var handleWrapper = new CommandHandler<T>(_bus, handler);
		_handleWrappers.Add(typeof(T), handleWrapper);
		Subscribe(handleWrapper, false);
		return new Disposer(() => { Unsubscribe(handler); return Unit.Default; });
	}

	public void Unsubscribe<T>(IHandleCommand<T> handler) where T : class, ICommand {
		if (!_handleWrappers.TryGetValue(typeof(T), out var wrapper))
			return;
		Unsubscribe((CommandHandler<T>)wrapper);
		_handleWrappers.Remove(typeof(T));
	}

	public void Publish(IMessage message)
		=> _queuedPublisher.Publish(message);

	public IDisposable Subscribe<T>(IHandle<T> handler, bool includeDerived = true) where T : class, IMessage
		=> _bus.Subscribe(handler, includeDerived);
	public IDisposable SubscribeToAll(IHandle<IMessage> handler)
		=> _bus.SubscribeToAll(handler);

	public void Unsubscribe<T>(IHandle<T> handler) where T : class, IMessage {
		_bus.Unsubscribe(handler);
	}

	public bool HasSubscriberFor<T>(bool includeDerived = false) where T : class, IMessage
		=> _bus.HasSubscriberFor<T>(includeDerived);

	public string Name => _bus.Name;

	public void Dispose() {
		Dispose(true);
		GC.SuppressFinalize(this);
	}
	protected virtual void Dispose(bool disposing) {
		if (_disposed)
			return;
		_disposed = true;
		if (disposing) {
			_queuedPublisher.Dispose();
		}
	}
}
