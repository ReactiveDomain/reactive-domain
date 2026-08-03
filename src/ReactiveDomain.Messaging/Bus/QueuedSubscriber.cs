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
	/// The message types currently registered on this subscriber — one entry per distinct type
	/// passed to a <c>Subscribe</c> overload, command registrations included (a command registers
	/// under the command type), and not the derived types each subscription also covers.
	/// </summary>
	/// <remarks>
	/// A read-only view of the subscription seam, so a test can ask the subscriber what it handles
	/// instead of scanning source for <c>Subscribe</c> calls. Each read is a fresh snapshot;
	/// registrations change only through <c>Subscribe</c> and disposal of what it returns.
	/// </remarks>
	public IReadOnlyCollection<Type> RegisteredMessageTypes => _internalBus.RegisteredMessageTypes;

	/// <summary>
	/// ReaderLock locks the message handlers and can be used when reading the subscriber's state
	/// to ensure that state is unchanged during the read.
	/// The lock should *not* be used in Handle methods as they are inside the lock already by default.
	/// </summary>
	protected readonly object ReaderLock = new();

	/// <summary>
	/// The version is equal to the number of messages dequeued to this subscriber.
	/// The version is incremented after all handlers have been processed, inside the
	/// <see cref="ReaderLock"/>, so a reader holding the lock always sees state and version agree.
	/// The number of handlers (including none) will not impact the version. Duplicate messages
	/// dropped by an idempotent subscriber are never dequeued and so do not advance it.
	/// This can be used to ensure subscriber state for tests.
	/// <para>Dequeues are counted per <em>registration</em>, not per published message. Each
	/// <c>Subscribe</c> call adds its own external-bus registration and the bus dispatches to
	/// every registration a message matches, so a subscriber registered for both a base type and
	/// a derived one enqueues — and so advances — twice for one derived message, and each of its
	/// handlers runs once per enqueue. Overlapping registrations on one subscriber are therefore
	/// not the shape to reach for; register each type once.</para>
	/// </summary>
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

	/// <summary>
	/// Every message handled by the subscriber passes through here. Dispatch order is the queue's,
	/// unchanged — the lock only excludes readers and, through the single queue thread, is
	/// uncontended on the dispatch path.
	/// </summary>
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
