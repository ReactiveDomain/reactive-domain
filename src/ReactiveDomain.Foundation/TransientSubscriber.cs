using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;

// ReSharper disable RedundantTypeArgumentsOfMethod

namespace ReactiveDomain.Foundation;

public abstract class TransientSubscriber : IDisposable {
	private readonly List<IDisposable> _subscriptions = [];
	private readonly List<(object Handler, Type MessageType, object Wrapper)> _wrappers = [];
	private readonly ISubscriber? _eventSubscriber;
	private readonly ICommandSubscriber? _commandSubscriber;

	protected TransientSubscriber(IDispatcher bus) : this((IBus)bus) {
		_commandSubscriber = bus ?? throw new ArgumentNullException(nameof(bus));
	}

	protected TransientSubscriber(IBus bus) : this((ISubscriber)bus) { }

	protected TransientSubscriber(ISubscriber subscriber) {
		_eventSubscriber = subscriber ?? throw new ArgumentNullException(nameof(subscriber));
	}

	protected TransientSubscriber(ICommandSubscriber subscriber) {
		_commandSubscriber = subscriber ?? throw new ArgumentNullException(nameof(subscriber));
	}

	/// <summary>
	/// Locks the message handlers. Hold it while reading the subscriber's state to see that state
	/// unchanged for the duration of the read.
	/// </summary>
	/// <remarks>
	/// Do <i>not</i> take it in a Handle method — handlers already run inside it. A handler holds it
	/// for its whole duration, so one that blocks waiting on work this subscriber must itself handle
	/// will deadlock.
	/// </remarks>
	protected readonly object ReaderLock = new();

	/// <summary>
	/// The number of handler invocations: a message matching two of this subscriber's registrations
	/// advances it twice.
	/// </summary>
	/// <remarks>
	/// Incremented inside the <see cref="ReaderLock"/> after the handler returns, so a reader holding
	/// the lock always sees state and version agree.
	/// </remarks>
	public int Version { get; private set; }

	protected void Subscribe<T>(IHandle<T> handler) where T : class, IMessage {
		if (_eventSubscriber == null)
			throw new InvalidOperationException("TransientSubscriber not created with EventBus to register on.");
		_subscriptions.Add(_eventSubscriber.Subscribe<T>(Serialized(handler)));
	}

	protected void Subscribe<T>(IHandleCommand<T> handler) where T : Command {
		if (_commandSubscriber == null)
			throw new InvalidOperationException("TransientSubscriber not created with CommandBus to register on.");
		_subscriptions.Add(_commandSubscriber.Subscribe<T>(Serialized(handler)));
	}

	/// <summary>
	/// One wrapper per handler and message type, so subscribing the same handler again is the same
	/// registration to the bus rather than a second one calling the same code.
	/// </summary>
	/// <remarks>
	/// Scanned rather than hashed: reference identity is what the bus matches on, and a hashed key
	/// would misbehave for a handler type that overrides <see cref="object.Equals(object)"/>.
	/// </remarks>
	private IHandle<T> Serialized<T>(IHandle<T> handler) where T : class, IMessage {
		if (Existing<T>(handler) is SerializedHandler<T> existing)
			return existing;
		var wrapper = new SerializedHandler<T>(this, handler);
		_wrappers.Add((handler, typeof(T), wrapper));
		return wrapper;
	}

	private IHandleCommand<T> Serialized<T>(IHandleCommand<T> handler) where T : Command {
		if (Existing<T>(handler) is SerializedCommandHandler<T> existing)
			return existing;
		var wrapper = new SerializedCommandHandler<T>(this, handler);
		_wrappers.Add((handler, typeof(T), wrapper));
		return wrapper;
	}

	private object? Existing<T>(object handler) {
		foreach (var entry in _wrappers)
			if (ReferenceEquals(entry.Handler, handler) && entry.MessageType == typeof(T))
				return entry.Wrapper;
		return null;
	}

	/// <summary>Runs a handler under <see cref="ReaderLock"/> and advances the version.</summary>
	/// <remarks>
	/// There is no queue here — dispatch is on whichever thread published — so this lock is the only
	/// thing serializing handlers against each other and against readers. Dispatch order is the
	/// publisher's; the wrapper calls straight through.
	/// </remarks>
	private void DispatchMessage(Action handle) {
		lock (ReaderLock) {
			handle();
			Version++;
		}
	}

	private CommandResponse DispatchCommand(Func<CommandResponse> handle) {
		lock (ReaderLock) {
			var response = handle();
			Version++;
			return response;
		}
	}

	private sealed class SerializedHandler<T>(TransientSubscriber owner, IHandle<T> handler)
		: IHandle<T> where T : class, IMessage {
		public void Handle(T message) => owner.DispatchMessage(() => handler.Handle(message));
	}

	private sealed class SerializedCommandHandler<T>(TransientSubscriber owner, IHandleCommand<T> handler)
		: IHandleCommand<T> where T : Command {
		public CommandResponse Handle(T command) => owner.DispatchCommand(() => handler.Handle(command));
	}

	public void Dispose() {
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	private bool _disposed;

	protected virtual void Dispose(bool disposing) {
		if (_disposed)
			return;
		if (disposing) {
			_subscriptions.ForEach(s => s.Dispose());
		}
		_disposed = true;
	}
}
