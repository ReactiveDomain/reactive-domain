using System.Reactive;
using ReactiveDomain.Util;

namespace ReactiveDomain.Messaging.Bus;

public abstract class QueuedSubscriber : IMessageRegistry, IDisposable {
	private readonly List<IDisposable> _subscriptions = [];
	private readonly Dictionary<Type, Feed> _feeds = [];
	private readonly object _feedLock = new();

	private readonly QueuedHandler _messageQueue;
	private readonly IBus _externalBus;
	private readonly InMemoryBus _internalBus;
	protected object? Last = null;
	public bool Starving => _messageQueue.Idle;

	/// <inheritdoc cref="IMessageRegistry.RegisteredMessageTypes"/>
	/// <remarks>Includes command registrations, which register under the command type.</remarks>
	public IReadOnlyCollection<Type> RegisteredMessageTypes => _internalBus.RegisteredMessageTypes;

	/// <inheritdoc cref="IMessageRegistry.HandledMessageTypes"/>
	/// <remarks>
	/// The types this subscriber's own handlers receive. It says nothing about what reaches its
	/// queue: the queue is fed by separate subscriptions on the external bus.
	/// </remarks>
	public IReadOnlyCollection<Type> HandledMessageTypes => _internalBus.HandledMessageTypes;

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
		return Register<T>(_internalBus.Subscribe(handler));
	}

	public IDisposable Subscribe<T>(IHandleCommand<T> handler) where T : class, ICommand {
		return Register<T>(_internalBus.Subscribe(new CommandHandler<T>(_externalBus, handler)));
	}

	/// <summary>The returned handle drops this registration and its share of the feed, nothing else.</summary>
	private IDisposable Register<T>(IDisposable internalSub) where T : class, IMessage {
		lock (_feedLock) {
			_subscriptions.Add(internalSub);
			Claim(typeof(T), () => _externalBus.Subscribe(new AdHocHandler<T>(_messageQueue.Handle)));
		}
		return new Disposer(() => {
			lock (_feedLock) {
				internalSub.Dispose();
				_subscriptions.Remove(internalSub);
				Release(typeof(T));
			}
			return Unit.Default;
		});
	}

	/// <summary>
	/// One external-bus subscription feeding the queue, shared by every handler declared for this
	/// type. <see cref="Attach"/> is a factory because the type is only known generically at
	/// <c>Subscribe</c>, and a feed dropped as covered may have to come back.
	/// </summary>
	private sealed class Feed {
		public Feed(Func<IDisposable> attach) => Attach = attach;
		public readonly Func<IDisposable> Attach;
		public IDisposable? Subscription;
		public int Handlers;
	}

	private void Claim(Type declared, Func<IDisposable> attach) {
		if (!_feeds.TryGetValue(declared, out var feed))
			_feeds.Add(declared, feed = new Feed(attach));
		feed.Handlers++;
		Reconcile();
	}

	private void Release(Type declared) {
		if (!_feeds.TryGetValue(declared, out var feed))
			return;
		feed.Handlers--;
		Reconcile();
	}

	/// <summary>
	/// Holds one subscription per <i>maximal</i> live type — one no other live type covers. A
	/// subscription per declared type would feed the queue once per registration a message matches;
	/// ancestors form a chain, so exactly one maximal type matches.
	/// </summary>
	/// <remarks>
	/// Two orderings this must keep. Recompute rather than patch — one claim can subsume several
	/// feeds and one release strand several. Attach before detaching — the reverse leaves an instant
	/// with nothing subscribed, turning a handover into a dropped message.
	/// </remarks>
	private void Reconcile() {
		var live = _feeds.Where(f => f.Value.Handlers > 0).Select(f => f.Key).ToArray();
		var wanted = _feeds
			.ToDictionary(f => f.Key,
				f => f.Value.Handlers > 0 && !live.Any(other => other != f.Key && Covers(other, f.Key)));

		foreach (var (type, feed) in _feeds)
			if (wanted[type] && feed.Subscription is null)
				feed.Subscription = feed.Attach();

		foreach (var (type, feed) in _feeds) {
			if (wanted[type] || feed.Subscription is null)
				continue;
			feed.Subscription.Dispose();
			feed.Subscription = null;
		}

		foreach (var spent in _feeds.Where(f => f.Value.Handlers <= 0).Select(f => f.Key).ToArray())
			_feeds.Remove(spent);
	}

	/// <summary>
	/// Whether a subscription declared for <paramref name="outer"/> also receives
	/// <paramref name="inner"/>. Equivalent to the <c>MessageHierarchy.DescendantsAndSelf</c>
	/// expansion the bus uses, but that walk is uncached and allocates, and this runs over every
	/// pair of declared types on each subscribe.
	/// </summary>
	private static bool Covers(Type outer, Type inner) => outer.IsAssignableFrom(inner);

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
		lock (_feedLock) {
			_subscriptions.ForEach(s => s.Dispose());
			_subscriptions.Clear();
			foreach (var feed in _feeds.Values)
				feed.Subscription?.Dispose();
			_feeds.Clear();
		}
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
