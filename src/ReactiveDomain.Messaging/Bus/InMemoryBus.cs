
// Based on InMemoryBus from EventStore LLP
// Added support for updating registered types and handlers from dynamically loaded assemblies
// Removed Unoptimized Bus
// See also changes in Message.cs 
// Chris Condron 3-4-2014

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable RedundantExtendsListEntry
// ReSharper disable ForCanBeConvertedToForeach

using System.Reactive;
using ReactiveDomain.Logging;
using ReactiveDomain.Util;

namespace ReactiveDomain.Messaging.Bus;

/// <summary>
/// Synchronously dispatches messages to zero or more subscribers.
/// Subscribers are responsible for handling exceptions
/// </summary>
public class InMemoryBus : IBus, ISubscriber, IPublisher, IHandle<IMessage>, IDisposable {

	public static InMemoryBus CreateTest() {
		return new InMemoryBus();
	}

	public static readonly TimeSpan DefaultSlowMessageThreshold = TimeSpan.FromMilliseconds(48);
	private static readonly ILogger _log = LogManager.GetLogger("ReactiveDomain");


	public string Name { get; }

	private readonly Dictionary<Type, List<IMessageHandler>> _handlers = new();

	private readonly bool _watchSlowMsg;
	private readonly TimeSpan _slowMsgThreshold;

	private InMemoryBus() : this("Test") { }

	public InMemoryBus(
		string name,
		bool watchSlowMsg = true,
		TimeSpan? slowMsgThreshold = null) {
		try {
			Name = name;
			_watchSlowMsg = watchSlowMsg;
			_slowMsgThreshold = slowMsgThreshold ?? DefaultSlowMessageThreshold;
		} catch (Exception ex) {
			if (_log.LogLevel >= LogLevel.Error)
				_log.ErrorException(ex, "Error building InMemoryBus");
			throw;
		}
	}

	/// <summary>
	/// Subscribes <paramref name="handler"/> to messages of type <typeparamref name="T"/>, and by
	/// default to types derived from it.
	/// </summary>
	/// <remarks>
	/// Subscribing the same handler again for the same <typeparamref name="T"/> is a no-op — a
	/// subscription is a set, not a count, so any one of the returned disposers releases it.
	/// Subscribing it for a <i>different</i> <typeparamref name="T"/> is a separate subscription:
	/// a handler registered for both a base and a derived type is called once through each.
	/// </remarks>
	public IDisposable Subscribe<T>(IHandle<T> handler, bool includeDerived = true) where T : class, IMessage {
		Ensure.NotNull(handler, "handler");
		Subscribe(new MessageHandler<T>(handler, handler.GetType().Name), handler, includeDerived);
		return new Disposer(() => { Unsubscribe(handler); return Unit.Default; });
	}

	private void Subscribe(IMessageHandler handler, object rawHandler, bool includeDerived) {
		var messageTypes = includeDerived
			? MessageHierarchy.DescendantsAndSelf(handler.MessageType).ToArray()
			: [handler.MessageType];
		for (var i = 0; i < messageTypes.Length; i++) {
			Subscribe(handler, rawHandler, messageTypes[i]);
		}
	}

	/// <summary>Subscribes <paramref name="handler"/> to every known message type.</summary>
	/// <remarks>
	/// Idempotent per handler; one that also subscribes to a type is called through both. The types
	/// are taken from the message hierarchy as it stands when this is called, so a type first seen
	/// afterwards — from an assembly loaded later — does not reach this handler.
	/// </remarks>
	public IDisposable SubscribeToAll(IHandle<IMessage> handler) {
		Ensure.NotNull(handler, "handler");
		var allHandler = new MessageHandler<IMessage>(handler, handler.GetType().Name);

		var messageTypes = MessageHierarchy.DescendantsAndSelf(typeof(object)).ToArray();

		for (var i = 0; i < messageTypes.Length; i++) {
			Subscribe(allHandler, handler, messageTypes[i]);
		}
		return new Disposer(() => { Unsubscribe(handler); return Unit.Default; });
	}

	/// <summary>
	/// Adds <paramref name="handler"/> to the handler list for <paramref name="messageType"/>, unless
	/// <paramref name="rawHandler"/> is already registered there for <paramref name="handler"/>'s own
	/// message type.
	/// </summary>
	/// <remarks>
	/// Duplicates are matched on <c>handler.MessageType</c>, not <paramref name="messageType"/>. This
	/// runs once per slot a registration covers, so matching the slot would read a base-type and a
	/// derived-type registration of one handler as duplicates of each other.
	/// </remarks>
	private void Subscribe(IMessageHandler handler, object rawHandler, Type messageType) {
		lock (_handlers) {
			if (!_handlers.TryGetValue(messageType, out var handleList)) {
				handleList = new List<IMessageHandler>();
				_handlers.Add(messageType, handleList);
			} else if (handleList.Any(hndl => hndl.IsSame(handler.MessageType, rawHandler))) {
				return;
			}

			handleList.Add(handler);
		}
	}

	public void Unsubscribe<T>(IHandle<T> handler) where T : class, IMessage {
		Ensure.NotNull(handler, "handler");
		var descendants = MessageHierarchy.DescendantsAndSelf(typeof(T)).ToArray();
		for (var d = 0; d < descendants.Length; d++) {
			var handlers = GetHandlesFor(descendants[d]);
			for (var h = 0; h < handlers.Length; h++) {
				if (!handlers[h].IsSame(typeof(T), handler))
					continue;
				lock (_handlers) {
					_handlers[descendants[d]].Remove(handlers[h]);
				}
				break;
			}
		}
	}
	/// <summary>
	/// The message types this bus has handlers registered for: one entry per distinct <c>T</c>
	/// passed to <see cref="Subscribe{T}"/> or <see cref="SubscribeToAll"/>, not the derived types
	/// a registration also fans out to (those share the registration's declared
	/// <see cref="IMessageHandler.MessageType"/>). Each read returns a fresh snapshot, so the
	/// registry itself is only ever changed by subscribing and unsubscribing.
	/// </summary>
	/// <remarks>
	/// Not cheap, and not for a hot path. It walks every registration in every type slot while holding
	/// the lock <see cref="Publish"/> also takes, so reading it repeatedly contends with publishing —
	/// and one <see cref="SubscribeToAll"/> puts a registration in every slot in the hierarchy.
	/// </remarks>
	public IReadOnlyCollection<Type> RegisteredMessageTypes {
		get {
			lock (_handlers) {
				return _handlers.Values
					.SelectMany(handlers => handlers)
					.Select(handler => handler.MessageType)
					.Distinct()
					.ToArray();
			}
		}
	}

	public bool HasSubscriberFor<T>(bool includeDerived = false) where T : class, IMessage {
		return HasSubscriberFor(typeof(T), includeDerived);
	}

	public bool HasSubscriberFor(Type type, bool includeDerived = false) {
		Type[] derivedTypes = [type];
		if (includeDerived) {
			derivedTypes = MessageHierarchy.DescendantsAndSelf(type).ToArray();
		}
		for (var i = 0; i < derivedTypes.Length; i++) {
			var derivedType = derivedTypes[i];
			if (HasSubscriberForExactType(derivedType)) {
				return true;
			}
		}
		return false;
	}
	protected bool HasSubscriberForExactType(Type type) {
		var handlers = GetHandlesFor(type);
		return handlers.Any(h => h.MessageType == type);
	}


	public void Handle(IMessage message) {
		Publish(message);
	}
	public void Publish(IMessage message) {
		// ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
		if (message == null) {
			_log.Error("Message was null, publishing aborted");
			return;
		}
		// Call each handler registered to the message type.
		var handlers = GetHandlesFor(message.GetType());
		for (int i = 0, n = handlers.Length; i < n; ++i) {
			var handler = handlers[i];

			if (_watchSlowMsg) {
				var before = DateTime.UtcNow;
				handler.TryHandle(message);

				var elapsed = DateTime.UtcNow - before;
				if (elapsed <= _slowMsgThreshold)
					continue;

				_log.Trace("SLOW BUS MSG [{0}]: {1} - {2}ms. Handler: {3}.",
					Name, message.GetType().Name, (int)elapsed.TotalMilliseconds, handler.HandlerName);
				if (elapsed > QueuedHandler.VerySlowMsgThreshold)// && !(message is SystemMessage.SystemInit))
					_log.Error("---!!! VERY SLOW BUS MSG [{0}]: {1} - {2}ms. Handler: {3}.",
						Name, message.GetType().Name, (int)elapsed.TotalMilliseconds, handler.HandlerName);
			} else {
				handler.TryHandle(message);
			}
		}
	}

	private IMessageHandler[] GetHandlesFor(Type type) {
		lock (_handlers) {
			return _handlers.TryGetValue(type, out var handlers) ? handlers.ToArray() : [];
		}
	}

	//tracing 
	public virtual void NoMessageHandler(dynamic msg, Type type) {
		_log.Info(type.Name + " message not handled (no handler)");
	}

	public virtual void PreHandleMessage(dynamic msg, Type type, IMessageHandler handler) {
		_log.Debug("{0} message handled by {1}", type.Name, handler.HandlerName);
	}

	public virtual void PostHandleMessage(dynamic msg, Type type, IMessageHandler handler, TimeSpan handleTimeSpan) {

	}

	public virtual void MessageReceived(dynamic msg, Type type, string publishedBy) {
		_log.Trace("Publishing Message {0} details \n{1}\n{2}", type.FullName, type.Name, Json.ToLogJson(msg));
	}

	//Implementation of IDisposable

	private bool _disposed;

	// Public implementation of Dispose pattern callable by consumers.
	public void Dispose() {
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	// Protected implementation of Dispose pattern.
	protected virtual void Dispose(bool disposing) {
		if (_disposed)
			return;

		if (disposing) {
			lock (_handlers) {
				_handlers.Clear();
			}
		}
		// Free any unmanaged objects here.
		//
		_disposed = true;
	}

}
