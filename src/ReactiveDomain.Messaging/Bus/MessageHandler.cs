namespace ReactiveDomain.Messaging.Bus;

public interface IMessageHandler {
	string HandlerName { get; }
	Type MessageType { get; }
	bool TryHandle(IMessage message);
	bool IsSame(Type messagesType, object handler);
}

public class MessageHandler<T> : IMessageHandler where T : class, IMessage {
	public string HandlerName { get; }

	public Type MessageType { get; }

	private readonly IHandle<T> _handler;

	public MessageHandler(IHandle<T> handler, string? handlerName) {
		_handler = handler ?? throw new ArgumentNullException(nameof(handler));
		HandlerName = handlerName ?? string.Empty;
		MessageType = typeof(T);
	}

	public bool TryHandle(IMessage message) {
		if (message is not T msg)
			return false;

		_handler.Handle(msg); //if this throws let it bubble up.
		return true;
	}

	public bool IsSame(Type messageType, object handler) {
		return ReferenceEquals(_handler, handler) && typeof(T) == messageType;
	}

	public override string ToString() {
		return string.IsNullOrEmpty(HandlerName) ? _handler.ToString() ?? "anonymous handler" : HandlerName;
	}
}
