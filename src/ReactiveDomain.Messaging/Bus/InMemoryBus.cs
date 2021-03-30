
// Based on InMemoryBus from EventStore LLP
// Added support for updating registered types and handlers from dynamically loaded assemblies
// Removed Unoptimized Bus
// See also changes in Message.cs 
// Chris Condron 3-4-2014

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable RedundantExtendsListEntry
// ReSharper disable ForCanBeConvertedToForeach

using System;
using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Logging;

using ReactiveDomain.Util;

namespace ReactiveDomain.Messaging.Bus
{

    /// <summary>
    /// Synchronously dispatches messages to zero or more subscribers.
    /// Subscribers are responsible for handling exceptions
    /// </summary>

    public class InMemoryBus : IBus, ISubscriber, IPublisher, IHandle<IMessage>, IDisposable
    {

        public static InMemoryBus CreateTest()
        {
            return new InMemoryBus();
        }

        public static readonly TimeSpan DefaultSlowMessageThreshold = TimeSpan.FromMilliseconds(48);
        private static readonly ILogger Log = Logging.LogProvider.GetLogger("ReactiveDomain");


        public string Name { get; }

        private Dictionary<Type, List<IMessageHandler>> _handlers = new Dictionary<Type, List<IMessageHandler>>();

        private readonly bool _watchSlowMsg;
        private readonly TimeSpan _slowMsgThreshold;

        private InMemoryBus() : this("Test") { }
        public InMemoryBus(
                    string name,
                    bool watchSlowMsg = true,
                    TimeSpan? slowMsgThreshold = null)
        {
            try
            {
                Name = name;
                _watchSlowMsg = watchSlowMsg;
                _slowMsgThreshold = slowMsgThreshold ?? DefaultSlowMessageThreshold;
            }
            catch (Exception ex)
            {
                //if (Log.LogLevel >= LogLevel.Error) Log.ErrorException(ex, "Error building InMemoryBus");
                Log.LogError(ex, "Error building InMemoryBus");
                throw;
            }

        }

        public IDisposable Subscribe<T>(IHandle<T> handler, bool includeDerived = true) where T : class, IMessage
        {
            Ensure.NotNull(handler, "handler");
            Subscribe(new MessageHandler<T>(handler, handler.GetType().Name), includeDerived);
            // ReSharper disable once ConstantConditionalAccessQualifier
            return new Disposer(() => { this?.Unsubscribe(handler); return Unit.Default; });
        }


        private void Subscribe(IMessageHandler handler, bool includeDerived)
        {
            Type[] messageTypes;
            if (includeDerived)
            {
                messageTypes = MessageHierarchy.DescendantsAndSelf(handler.MessageType).ToArray();
            }
            else
            {
                messageTypes = new[] { handler.MessageType };
            }
            for (var i = 0; i < messageTypes.Length; i++)
            {
                Subcribe(handler, messageTypes[i]);
            }
        }
        public IDisposable SubscribeToAll(IHandle<IMessage> handler)
        {
            Ensure.NotNull(handler, "handler");
            var allHandler = new MessageHandler<IMessage>(handler, handler.GetType().Name);
           
            var messageTypes = MessageHierarchy.DescendantsAndSelf(typeof(object)).ToArray();

            for (var i = 0; i < messageTypes.Length; i++)
            {
                Subcribe(allHandler, messageTypes[i]);
            }
            return new Disposer(() => { this?.Unsubscribe(handler); return Unit.Default; });
        }
        private void Subcribe(IMessageHandler handler, Type messageType)
        {
            var handlers = GetHandlesFor(messageType);
            if (!handlers.Any(hndl => hndl.IsSame(messageType, handler)))
            {
                lock (_handlers)
                {
                    if (!_handlers.TryGetValue(messageType, out var handleList))
                    {
                        handleList = new List<IMessageHandler>();
                        _handlers.Add(messageType, handleList);
                    }

                    handleList.Add(handler);
                }
            }
        }

        public void Unsubscribe<T>(IHandle<T> handler) where T : class, IMessage
        {
            Ensure.NotNull(handler, "handler");
            var descendants = MessageHierarchy.DescendantsAndSelf(typeof(T)).ToArray();
            for (var d = 0; d < descendants.Length; d++)
            {
                var handlers = GetHandlesFor(descendants[d]);
                for (var h = 0; h < handlers.Length; h++)
                {
                    if (!handlers[h].IsSame(typeof(T), handler)) continue;
                    lock (_handlers)
                    {
                        _handlers[descendants[d]].Remove(handlers[h]);
                    }
                    break;
                }
            }
        }
        public bool HasSubscriberFor<T>(bool includeDerived = false) where T : class, IMessage
        {
            return HasSubscriberFor(typeof(T), includeDerived);
        }
        protected bool HasSubscriberFor(Type type, bool includeDerived)
        {
            Type[] derivedTypes = { type };
            if (includeDerived)
            {
                derivedTypes = MessageHierarchy.DescendantsAndSelf(type).ToArray();
            }
            for (var i = 0; i < derivedTypes.Length; i++)
            {
                var derivedType = derivedTypes[i];
                if (HasSubscriberForExactType(derivedType))
                {
                    return true;
                }
            }
            return false;
        }
        protected bool HasSubscriberForExactType(Type type)
        {
            var handlers = GetHandlesFor(type);
            return handlers.Any(h => h.MessageType == type);
        }


        public void Handle(IMessage message)
        {
            Publish(message);
        }
        public void Publish(IMessage message)
        {
            if (message == null)
            {
                Log.LogError("Message was null, publishing aborted");
                return;
            }
            // Call each handler registered to the message type.
            var handlers = GetHandlesFor(message.GetType());
            for (int i = 0, n = handlers.Length; i < n; ++i)
            {
                var handler = handlers[i];

                if (_watchSlowMsg)
                {
                    var before = DateTime.UtcNow;
                    handler.TryHandle(message);

                    var elapsed = DateTime.UtcNow - before;
                    if (elapsed <= _slowMsgThreshold) continue;

                    Log.LogTrace("SLOW BUS MSG [{0}]: {1} - {2}ms. Handler: {3}.",
                        Name, message.GetType().Name, (int)elapsed.TotalMilliseconds, handler.HandlerName);
                    if (elapsed > QueuedHandler.VerySlowMsgThreshold)// && !(message is SystemMessage.SystemInit))
                        Log.LogError("---!!! VERY SLOW BUS MSG [{0}]: {1} - {2}ms. Handler: {3}.",
                            Name, message.GetType().Name, (int)elapsed.TotalMilliseconds, handler.HandlerName);
                }
                else
                {
                    handler.TryHandle(message);
                }
            }
        }

        private IMessageHandler[] GetHandlesFor(Type type)
        {
            lock (_handlers)
            {
                if (_handlers.TryGetValue(type, out var handlers))
                {
                    return handlers.ToArray();
                }
                return new IMessageHandler[] { };
            }
        }

        //tracing 
        public virtual void NoMessageHandler(dynamic msg, Type type)
        {
            Log.LogInformation(type.Name + " message not handled (no handler)");
        }

        public virtual void PreHandleMessage(dynamic msg, Type type, IMessageHandler handler)
        {
            Log.LogDebug("{0} message handled by {1}", type.Name, handler.HandlerName);
        }

        public virtual void PostHandleMessage(dynamic msg, Type type, IMessageHandler handler, TimeSpan handleTimeSpan)
        {

        }

        public virtual void MessageReceived(dynamic msg, Type type, string publishedBy)
        {
            //Log.Trace("Publishing Message {0} details \n{1}\n{2}", type.FullName, type.Name, Json.ToLogJson(msg));
            //dealproc: I had an issue with logtrace not handling ^^ appropriately, so i've inlined the data.
            Log.LogTrace($"Publishing Message {type.FullName} details \n{type.Name}\n{Json.ToLogJson(msg)}");
        }

        //Implementation of IDisposable

        private bool _disposed;

        // Public implementation of Dispose pattern callable by consumers.
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                lock (_handlers)
                {
                    _handlers.Clear();
                }
            }
            // Free any unmanaged objects here.
            //
            _disposed = true;
        }

    }

}