using System.Reactive;
using System.Reflection;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Util;

namespace ReactiveDomain.Testing.Messaging;

/// <summary>
/// Extends <see cref="CapturingBus"/> to enable subscribers to commands and events, and delivers published
/// messages and sent commands to subscribers via an internal <see cref="InMemoryBus"/>. Message delivery is
/// synchronous.
/// </summary>
public sealed class CapturingSubscribableBus : IDispatcher {
	private readonly SingleThreadedBus _bus = new();
	private readonly Dictionary<Type, object> _handlerWrappers = new();
	private readonly List<ICommand> _sentCommands = [];
	private readonly List<IMessage> _publishedMessages = [];
	private readonly List<IMessage> _allMessages = [];

	/// <summary>
	/// Gets a read-only list of all commands that have been sent.
	/// </summary>
	public IReadOnlyList<ICommand> SentCommands => _sentCommands.AsReadOnly();
	/// <summary>
	/// Gets a read-only list of all messages that have been published.
	/// </summary>
	public IReadOnlyList<IMessage> PublishedMessages => _publishedMessages.AsReadOnly();
	/// <summary>
	/// Gets a read-only list of all messages that have been published or sent.
	/// </summary>
	public IReadOnlyList<IMessage> AllMessages => _allMessages.AsReadOnly();

	/// <summary>
	/// Clears all lists of published and sent messages.
	/// </summary>
	public void ClearMessages() {
		_sentCommands.Clear();
		_publishedMessages.Clear();
		_allMessages.Clear();
	}

	public string Name => nameof(CapturingSubscribableBus);

	public bool Idle => true;

	public IDisposable Subscribe<T>(IHandle<T> handler, bool includeDerived = true) where T : class, IMessage {
		return _bus.Subscribe(handler, includeDerived);
	}

	public IDisposable Subscribe<T>(IHandleCommand<T> handler) where T : class, ICommand {
		if (HasSubscriberFor<T>())
			throw new ExistingHandlerException("Duplicate registration for command type.");
		var wrapper = new DirectCommandHandler<T>(handler);
		_handlerWrappers.Add(typeof(T), wrapper);
		Subscribe(wrapper, false);
		return new Disposer(() => {
			Unsubscribe(handler);
			return Unit.Default;
		});
	}

	public IDisposable SubscribeToAll(IHandle<IMessage> handler) => _bus.SubscribeToAll(handler);

	public bool HasSubscriberFor<T>(bool includeDerived = false) where T : class, IMessage =>
		_bus.HasSubscriberFor<T>(includeDerived);

	public void Unsubscribe<T>(IHandle<T> handler) where T : class, IMessage => _bus.Unsubscribe(handler);

	public void Unsubscribe<T>(IHandleCommand<T> handler) where T : class, ICommand {
		if (!_handlerWrappers.TryGetValue(typeof(T), out var wrapper))
			return;
		Unsubscribe((DirectCommandHandler<T>)wrapper);
		_handlerWrappers.Remove(typeof(T));
	}

	public void Publish(IMessage message) {
		if (message is not (ICommand or AckCommand or CommandResponse)) {
			_publishedMessages.Add(message);
			_allMessages.Add(message);
		}
		_bus.Publish(message);
	}

	public void Send(ICommand command, string? exceptionMsg = null, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null) {
		_sentCommands.Add(command);
		_allMessages.Add(command);
		responseTimeout ??= TimeSpan.FromSeconds(5); // Default timeout long enough for heavy load on slow test machines
		if (!_handlerWrappers.TryGetValue(command.GetType(), out var handler)) {
			// If there's a subscriber to this command type that uses an internal queue, then it will have subscribed
			// using IHandle<T> and will not handle synchronously. In those cases we publish on the internal bus, and
			// then wait for a CommandResponse to be published onto that bus.
			if (_bus.HasSubscriberFor(command.GetType())) {
				var response = AwaitResponse(command, responseTimeout.Value)
							   ?? throw new CommandTimedOutException(command);
				if (response is Fail f)
					throw new CommandException("Command failed", f.Exception, command);
			} else
				throw new CommandTimedOutException("Could not find a handler", command);
		} else {
			try {
				handler.GetType().GetMethod("Handle")?.Invoke(handler, [command]);
			} catch (TargetInvocationException ex) {
				throw new CommandException("Command failed", ex.InnerException, command);
			}
		}
	}

	/// <summary>
	/// Publishes the command and waits for THIS command's response, or null on timeout.
	/// The watcher must filter by <see cref="CommandResponse.CommandId"/>: with queued
	/// subscribers a handler can still be running (and can send nested commands) after its
	/// caller's wait begins, so an unfiltered watcher is completed by whichever response
	/// happens to be published first — including a straggler from a previous command —
	/// manufacturing phantom results for commands that were never even dequeued.
	/// </summary>
	private CommandResponse? AwaitResponse(ICommand command, TimeSpan responseTimeout) {
		CommandResponse? response = null;
		using var d = _bus.Subscribe(new AdHocHandler<CommandResponse>(r => {
			if (r.CommandId == command.MsgId)
				Volatile.Write(ref response, r);
		}));
		_bus.Publish(command);
		// SpinUntil yields/sleeps between probes (the previous loop hot-spun a full core, which
		// on a 2-core CI runner starved the very handler thread it was waiting on).
		SpinWait.SpinUntil(() => Volatile.Read(ref response) is not null, responseTimeout);
		return Volatile.Read(ref response);
	}

	public bool TrySend(ICommand command, out CommandResponse response, TimeSpan? responseTimeout = null,
		TimeSpan? ackTimeout = null) {
		_sentCommands.Add(command);
		_allMessages.Add(command);
		responseTimeout ??= TimeSpan.FromSeconds(5); // Default timeout long enough for heavy load on slow test machines
		if (!_handlerWrappers.TryGetValue(command.GetType(), out var handler)) {
			// If there's a subscriber to this command type that uses an internal queue, then it will have subscribed
			// using IHandle<T> and will not handle synchronously. In those cases we publish on the internal bus, and
			// then wait for a CommandResponse to be published onto that bus.
			if (_bus.HasSubscriberFor(command.GetType())) {
				var resp = AwaitResponse(command, responseTimeout.Value);
				if (resp is null) {
					response = command.Fail(new CommandTimedOutException(command));
					return false;
				}
				if (resp is Fail f) {
					response = command.Fail(f.Exception);
					return false;
				}

				response = resp;
				return true;
			}

			response = command.Fail(new CommandTimedOutException("Could not find a handler", command));
			return false;
		}

		try {
			var directHandler = handler.GetType();
			directHandler.GetMethod("Handle")?.Invoke(handler, [command]);
			response = (CommandResponse)directHandler.GetProperty("Response")!.GetValue(handler)!;
		} catch (Exception ex) {
			response = command.Fail(ex);
		}
		return response is Success;
	}

	public bool TrySendAsync(ICommand command, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null) {
		_sentCommands.Add(command);
		_allMessages.Add(command);
		responseTimeout ??= TimeSpan.FromSeconds(5); // Default timeout long enough for heavy load on slow test machines
		if (!_handlerWrappers.TryGetValue(command.GetType(), out var handler)) {
			var response = AwaitResponse(command, responseTimeout.Value);
			return response is not (null or Fail);
		}
		try {
			handler.GetType().GetMethod("Handle")?.Invoke(handler, [command]);
		} catch (Exception) {
			return false;
		}
		return true;
	}

	public void Dispose() {
		_handlerWrappers.Clear();
		_bus.Dispose();
	}

	private class DirectCommandHandler<T>(IHandleCommand<T> handler) : IHandle<T> where T : class, ICommand {
		public CommandResponse? Response { get; private set; }

		public void Handle(T message) {
			Response = handler.Handle(message);
		}
	}
}
