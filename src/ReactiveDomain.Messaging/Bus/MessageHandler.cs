using System;

namespace ReactiveDomain.Messaging.Bus
{
    public interface IMessageHandler
    {
        string HandlerName { get; }
        Type MessageType { get; }
        bool TryHandle(Message message);
        bool IsSame<T>(object handler);
    }
    
    internal class MessageHandler<T> : IMessageHandler where T : Message
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

        public bool TryHandle(Message message)
        {
            var msg = message as T;
            if (msg == null) return false;

            _handler.Handle(msg); //if this throws let it bubble up.
            return true;
        }

        public bool IsSame<T2>(object handler)
        {
            return ReferenceEquals(_handler, handler) && typeof(T) == typeof(T2);
        }

        public override string ToString()
        {
            return string.IsNullOrEmpty(HandlerName) ? _handler.ToString() : HandlerName;
        }
    }
}