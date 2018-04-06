
// Based on InMemoryBus from EventStore LLP
// Added support for updating registered types and hendlers from dynamicly loaded assemblies
// Removed Unoptimized Bus
// See also changes in Message.cs 
// Chris Condron 3-4-2014

// ReSharper disable MemberCanBePrivate.Global
// ReSharper disable RedundantExtendsListEntry
// ReSharper disable ForCanBeConvertedToForeach

using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveDomain.Logging;
using ReactiveDomain.Util;

namespace ReactiveDomain.Messaging.Bus
{

    #region InMemoryBus
    /// <summary>
    /// Synchronously dispatches messages to zero or more subscribers.
    /// Subscribers are responsible for handling exceptions
    /// </summary>

    public class InMemoryBus : IBus, ISubscriber, IPublisher, IHandle<Message>, IDisposable
    {

        public static InMemoryBus CreateTest()
        {
            return new InMemoryBus();
        }

        public static readonly TimeSpan DefaultSlowMessageThreshold = TimeSpan.FromMilliseconds(48);
        private static readonly ILogger Log = LogManager.GetLogger("ReactiveDomain");


        public string Name { get; private set; }

        private Dictionary<Type, List<IMessageHandler>> _handlers = new Dictionary<Type, List<IMessageHandler>>();

        private readonly bool _watchSlowMsg;
        private readonly TimeSpan _slowMsgThreshold;

        private InMemoryBus()
            : this("Test")
        {
        }

        public InMemoryBus(string name, bool watchSlowMsg = true, TimeSpan? slowMsgThreshold = null)
        {
            try
            {
                Name = name;
                _watchSlowMsg = watchSlowMsg;
                _slowMsgThreshold = slowMsgThreshold ?? DefaultSlowMessageThreshold;

                MessageHierarchy.MessageTypesAdded += MessageHierarchy_MessageTypesAdded;

                //_handlers = new List<IMessageHandler>[MessageHierarchy.MaxMsgTypeId + 1];
                //for (int i = 0; i < _handlers.Length; ++i)
                //{
                //    _handlers[i] = new List<IMessageHandler>();
                //}
            }
            catch (Exception ex)
            {
                if (Log.LogLevel >= LogLevel.Error) Log.ErrorException(ex, "Error building InMemoryBus");
                throw;
            }

        }

        void MessageHierarchy_MessageTypesAdded(object sender, EventArgs e)
        {

            var registeredHandlers = new HashSet<IMessageHandler>();
            lock (_handlers)
            {
                foreach (var msgHandler in _handlers.Values.SelectMany(h => h))
                {
                    registeredHandlers.Add(msgHandler);
                }

                _handlers = new Dictionary<Type, List<IMessageHandler>>();
                //for (int i = 0; i < _handlers.Length; ++i) //Initialize the new array
                //{
                //    _handlers[i] = new List<IMessageHandler>();
                //}

                foreach (var registeredHandler in registeredHandlers)
                {
                    Subscribe(registeredHandler);
                }
            }

        }

        private void Subscribe(IMessageHandler registeredHandler)
        {
            // Subscribe to the underlying message type and it's descendants
            var descendants = MessageHierarchy.DescendantsAndSelf(registeredHandler.MessageType);
            foreach (var descendant in descendants)
            {
                List<IMessageHandler> handlers;
                if (!_handlers.TryGetValue(descendant, out handlers))
                {
                    handlers = new List<IMessageHandler>();
                    _handlers[descendant] = handlers;
                }
                if (handlers.All(x => x.MessageType != registeredHandler.MessageType))
                    handlers.Add(registeredHandler);
            }
        }

        public IDisposable Subscribe<T>(IHandle<T> handler) where T : Message
        {
            Ensure.NotNull(handler, "handler");
            lock (_handlers)
            {
                var descendants = MessageHierarchy.DescendantsAndSelf(typeof(T));
                foreach (var descendant in descendants)
                {
                    List<IMessageHandler> handlers;
                    if (!_handlers.TryGetValue(descendant, out handlers))
                    {
                        handlers = new List<IMessageHandler>();
                        _handlers[descendant] = handlers;
                    }
                    if (!handlers.Any(x => x.IsSame<T>(handler)))
                        handlers.Add(new MessageHandler<T>(handler, handler.GetType().Name));
                }
                return new Disposer(() => { this?.Unsubscribe(handler); return Unit.Default; });
            }
        }

        public void Unsubscribe<T>(IHandle<T> handler) where T : Message
        {
            Ensure.NotNull(handler, "handler");
            lock (_handlers)
            {
                var descendants = MessageHierarchy.DescendantsAndSelf(typeof(T));
                foreach (var descendant in descendants)
                {
                    List<IMessageHandler> handlers;
                    if (!_handlers.TryGetValue(descendant, out handlers))
                    {
                        handlers = new List<IMessageHandler>();
                        _handlers[descendant] = handlers;
                    }
                    var messageHandler = handlers?.FirstOrDefault(x => x.IsSame<T>(handler));
                    if (messageHandler != null)
                        handlers.Remove(messageHandler);
                }
            }
        }
        protected bool HasSubscriberFor(Type type, bool includeDerived = false)
        {
            if (_handlers.TryGetValue(type, out List<IMessageHandler> handlers))
            {
                return handlers.Any(h => includeDerived || h.MessageType == type);
            }
            return false;
        }
        public bool HasSubscriberFor<T>(bool includeDerived = false) where T : Message
        {
            return HasSubscriberFor(typeof(T), includeDerived);
        }

        public void Handle(Message message)
        {
            Publish(message);
        }

        public virtual void NoMessageHandler(dynamic msg, Type type)
        {
            Log.Info(type.Name + " message not handled (no handler)");
        }

        public virtual void PreHandleMessage(dynamic msg, Type type, IMessageHandler handler)
        {
            Log.Debug("{0} message handled by {1}", type.Name, handler.HandlerName);
        }

        public virtual void PostHandleMessage(dynamic msg, Type type, IMessageHandler handler, TimeSpan handleTimeSpan)
        {

        }

        public virtual void MessageReceived(dynamic msg, Type type, string publishedBy)
        {
            //Log.Trace("Publishing Message {0} details \n{1}\n{2}",type.FullName, type.Name, Json.ToLogJson(@event));
        }

        public void Publish(Message message)
        {
            if (message == null)
            {
                Log.Error("Message was null, publishing aborted");
                return;
            }

            var handlers = _handlers[message.GetType()];


            for (int i = 0, n = handlers.Count; i < n; ++i)
            {
                var handler = handlers[i];

                if (_watchSlowMsg)
                {
                    var before = DateTime.UtcNow;
                    handler.TryHandle(message);

                    var elapsed = DateTime.UtcNow - before;
                    if (elapsed <= _slowMsgThreshold) continue;

                    Log.Trace("SLOW BUS MSG [{0}]: {1} - {2}ms. Handler: {3}.",
                        Name, message.GetType().Name, (int)elapsed.TotalMilliseconds, handler.HandlerName);
                    if (elapsed > QueuedHandler.VerySlowMsgThreshold)// && !(message is SystemMessage.SystemInit))
                        Log.Error("---!!! VERY SLOW BUS MSG [{0}]: {1} - {2}ms. Handler: {3}.",
                            Name, message.GetType().Name, (int)elapsed.TotalMilliseconds, handler.HandlerName);
                }
                else
                {
                    handler.TryHandle(message);
                }
            }

        }
        #region Implementation of IDisposable

        private bool _disposed = false;

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
        #endregion
    }

    #endregion
}