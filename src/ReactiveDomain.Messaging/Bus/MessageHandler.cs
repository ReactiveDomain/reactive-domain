using System;

namespace ReactiveDomain.Messaging.Bus
{
    public interface IMessageHandler
    {
        string HandlerName { get; }
        Type MessageType { get; }
        bool TryHandle(IMessage message);
        bool IsSame(Type messagesType, object handler);
    }
    
    internal class MessageHandler<T> : IMessageHandler where T : class, IMessage
    {
        public string HandlerName { get; private set; }

        public Type MessageType { get; private set; }

        private readonly IHandle<T> _handler;

        internal MessageHandler(IHandle<T> handler, string handlerName)
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));
            _handler = handler;
            HandlerName = handlerName ?? string.Empty;
            MessageType = typeof(T);
        }

        public bool TryHandle(IMessage message)
        {
            var msg = message as T;
            if (msg == null) return false;

            _handler.Handle(msg); //if this throws let it bubble up.
            return true;
        }

        public bool IsSame(Type messageType, object handler)
        {
            return ReferenceEquals(_handler, handler) && typeof(T) == messageType;
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(HandlerName) ? _handler.ToString() : HandlerName;
        }
    }
}