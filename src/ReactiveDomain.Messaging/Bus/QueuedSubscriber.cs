using System.Reactive;
using ReactiveDomain.Util;

namespace ReactiveDomain.Messaging.Bus;

public abstract class QueuedSubscriber : IDisposable {
	private readonly List<IDisposable> _subscriptions = [];

	private readonly QueuedHandler _messageQueue;
	private readonly IBus _externalBus;
	private readonly InMemoryBus _internalBus;
	protected object? Last = null;
	public bool Starving => _messageQueue.Idle;

	/// <summary>
	/// Locks the message handlers. Hold it while reading the subscriber's state to see that state
	/// unchanged for the duration of the read.
	/// </summary>
	/// <remarks>
	/// Do <i>not</i> take it in a Handle method — handlers already run inside it.
	/// </remarks>
	protected readonly object ReaderLock = new();

	/// <summary>
	/// The number of messages dequeued to this subscriber, whatever the handler count — including none.
	/// </summary>
	/// <remarks>
	/// A duplicate dropped by an idempotent subscriber does not advance the version. Incremented
	/// inside the <see cref="ReaderLock"/> after the handlers run, so a reader holding the lock
	/// always sees state and version agree.
	/// </remarks>
	public int Version { get; private set; }

	protected QueuedSubscriber(IBus bus, bool idempotent = false) {
		_externalBus = bus ?? throw new ArgumentNullException(nameof(bus));
		_internalBus = new InMemoryBus("SubscriptionBus");

		if (idempotent)
			_messageQueue = new QueuedHandler(
				new IdempotentHandler<IMessage>(
					new AdHocHandler<IMessage>(DequeueMessage)
				),
				"SubscriptionQueue");
		else
			_messageQueue = new QueuedHandler(
				new AdHocHandler<IMessage>(DequeueMessage),
				"SubscriptionQueue");
		_messageQueue.Start();
	}

	/// <summary>Every message handled by the subscriber passes through here.</summary>
	/// <remarks>
	/// Dispatch order is the queue's — the lock only excludes readers and, through the single queue
	/// thread, is uncontended on the dispatch path.
	/// </remarks>
	private void DequeueMessage(IMessage message) {
		lock (ReaderLock) {
			_internalBus.Publish(message);
			Version++;
		}
	}

	public IDisposable Subscribe<T>(IHandle<T> handler) where T : class, IMessage {
		var internalSub = _internalBus.Subscribe(handler);
		var externalSub = _externalBus.Subscribe(new AdHocHandler<T>(_messageQueue.Handle));
		_subscriptions.Add(internalSub);
		_subscriptions.Add(externalSub);
		return new Disposer(() => {
			internalSub.Dispose();
			externalSub.Dispose();
			return Unit.Default;
		});
	}

	public IDisposable Subscribe<T>(IHandleCommand<T> handler) where T : class, ICommand {
		var internalSub = _internalBus.Subscribe(new CommandHandler<T>(_externalBus, handler));
		var externalSub = _externalBus.Subscribe(new AdHocHandler<T>(_messageQueue.Handle));
		_subscriptions.Add(internalSub);
		_subscriptions.Add(externalSub);
		return new Disposer(() => {
			internalSub.Dispose();
			externalSub.Dispose();
			return Unit.Default;
		});
	}

	public void Dispose() {
		StopMessagePump();
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	private bool _disposed;
	private bool _pumpStopped;

	/// <summary>
	/// Stops message intake and processing: unsubscribes so nothing new is enqueued, then
	/// joins the queue thread. Runs ahead of the virtual dispose chain (which tears down
	/// derived state) so that no handler can be dispatched into state a derived subscriber
	/// has already disposed. Idempotent.
	/// </summary>
	private void StopMessagePump() {
		if (_pumpStopped)
			return;
		_subscriptions.ForEach(s => s.Dispose());
		_messageQueue.Stop();
		_pumpStopped = true;
	}

	protected virtual void Dispose(bool disposing) {
		if (_disposed)
			return;
		if (disposing) {
			StopMessagePump();
			_internalBus.Dispose();
			_disposed = true;
		}
	}
}
