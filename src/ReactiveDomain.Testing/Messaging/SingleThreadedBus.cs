using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;

namespace ReactiveDomain.Testing.Messaging;

/// <summary>
/// An <see cref="IBus"/> that calls handlers synchronously on Publish.
/// </summary>
internal class SingleThreadedBus : IBus, IDisposable {
    private readonly Dictionary<Type, List<IMessageHandler>> _handlers = [];

    public string Name => "Test";

    /// <summary>
    /// Publishes the specified message to all registered handlers for its type.
    /// </summary>
    /// <remarks>If no handlers are registered for the message type, the method completes without effect.
    /// Handlers are invoked in the order they are returned by the handler registry. This method does not
    /// handle exceptions thrown by handlers.</remarks>
    /// <param name="message">The message to be published. Cannot be null. The message is dispatched to all handlers that are registered for
    /// its runtime type.</param>
    public void Publish(IMessage message) {
        var handlers = GetHandlersFor(message.GetType());
        foreach (var handler in handlers)
            handler.TryHandle(message);
    }

    /// <summary>
    /// Subscribes the specified handler to receive messages of type T.
    /// </summary>
    /// <remarks>Disposing the returned IDisposable will unsubscribe the handler. If includeDerived is true,
    /// the handler will receive messages of type T and any types derived from T.</remarks>
    /// <typeparam name="T">The type of message to subscribe to. Must be a reference type implementing IMessage.</typeparam>
    /// <param name="handler">The handler that will be invoked when a message of type T is published. Cannot be null.</param>
    /// <param name="includeDerived">true to also receive messages of types derived from T; otherwise, false. The default is true.</param>
    /// <returns>An IDisposable that can be used to unsubscribe the handler from receiving messages.</returns>
    public IDisposable Subscribe<T>(IHandle<T> handler, bool includeDerived = true) where T : class, IMessage {
        Subscribe(new MessageHandler<T>(handler, handler.GetType().Name), includeDerived);
        return new Disposer(() => {
            Unsubscribe(handler);
            return Unit.Default;
        });
    }

    private void Subscribe(IMessageHandler handler, bool includeDerived) {
        var messageTypes = includeDerived
            ? MessageHierarchy.DescendantsAndSelf(handler.MessageType).ToArray()
            : [handler.MessageType];
        foreach (var t in messageTypes) {
            Subscribe(handler, t);
        }
    }

    private void Subscribe(IMessageHandler handler, Type messageType) {
        var handlers = GetHandlersFor(messageType);
        if (!handlers.Any(h => h.IsSame(messageType, handler))) {
            lock (_handlers) {
                if (!_handlers.TryGetValue(messageType, out var handleList)) {
                    handleList = [];
                    _handlers[messageType] = handleList;
                }
                handleList.Add(handler);
            }
        }
    }

    private IMessageHandler[] GetHandlersFor(Type type) {
        lock (_handlers) {
            return _handlers.TryGetValue(type, out var handlers) ? handlers.ToArray() : [];
        }
    }

    /// <summary>
    /// Subscribes the specified message handler to receive all message types published by the system.
    /// </summary>
    /// <remarks>Disposing the returned object will remove the handler from all subscriptions. This method is
    /// thread-safe.</remarks>
    /// <param name="handler">The handler that will receive all messages. Cannot be null.</param>
    /// <returns>An object that can be disposed to unsubscribe the handler from all message types.</returns>
    public IDisposable SubscribeToAll(IHandle<IMessage> handler) {
        var allHandler = new MessageHandler<IMessage>(handler, handler.GetType().Name);
        var messageTypes = MessageHierarchy.DescendantsAndSelf(typeof(object)).ToArray();
        foreach (var t in messageTypes)
            Subscribe(allHandler, t);
        return new Disposer(() => {
            Unsubscribe(handler);
            return Unit.Default;
        });
    }

    /// <summary>
    /// Unsubscribes the specified handler from receiving messages of type T and its derived types.
    /// </summary>
    /// <remarks>If the handler is subscribed to multiple message types in the hierarchy of T, it will be
    /// unsubscribed from all of them. This method is thread-safe.</remarks>
    /// <typeparam name="T">The message type to unsubscribe from. Must be a reference type that implements IMessage.</typeparam>
    /// <param name="handler">The handler to remove from the subscription list for messages of type T.</param>
    public void Unsubscribe<T>(IHandle<T> handler) where T : class, IMessage {
        var descendants = MessageHierarchy.DescendantsAndSelf(typeof(T)).ToArray();
        foreach (var d in descendants) {
            var handlers = GetHandlersFor(d);
            foreach (var h in handlers) {
                if (!h.IsSame(typeof(T), handler)) continue;
                lock (_handlers) {
                    _handlers[d].Remove(h);
                }
                break;
            }
        }
    }

    /// <summary>
    /// Determines whether there is at least one subscriber for messages of the specified type.
    /// </summary>
    /// <typeparam name="T">The type of message to check for subscribers. Must be a reference type that implements IMessage.</typeparam>
    /// <param name="includeDerived">true to include subscribers for derived message types; otherwise, false.</param>
    /// <returns>true if there is at least one subscriber for the specified message type; otherwise, false.</returns>
    public bool HasSubscriberFor<T>(bool includeDerived = false) where T : class, IMessage {
        return HasSubscriberFor(typeof(T), includeDerived);
    }

    /// <summary>
    /// Determines whether there is at least one subscriber for messages of the specified type.
    /// </summary>
    /// <param name="type">The type of message to check for subscribers. Must be a reference type that implements IMessage.</param>
    /// <param name="includeDerived">true to include subscribers for derived message types; otherwise, false.</param>
    /// <returns>true if there is at least one subscriber for the specified message type; otherwise, false.</returns>
    public bool HasSubscriberFor(Type type, bool includeDerived = false) {
        var derivedTypes = includeDerived ? MessageHierarchy.DescendantsAndSelf(type).ToArray() : [type];
        return derivedTypes.Any(HasSubscriberForExactType);
    }

    protected bool HasSubscriberForExactType(Type type) {
        var handlers = GetHandlersFor(type);
        return handlers.Any(h => h.MessageType == type);
    }

    /// <summary>
    /// Releases all resources used by the current instance.
    /// </summary>
    public void Dispose() {
        lock (_handlers) {
            _handlers.Clear();
        }
    }
}
