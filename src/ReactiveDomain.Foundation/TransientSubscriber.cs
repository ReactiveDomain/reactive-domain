using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;

// ReSharper disable RedundantTypeArgumentsOfMethod

namespace ReactiveDomain.Foundation;

public abstract class TransientSubscriber : IDisposable {
	private readonly List<IDisposable> _subscriptions = [];
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
	/// ReaderLock locks the message handlers and can be used when reading the subscriber's state
	/// to ensure that state is unchanged during the read.
	/// The lock should *not* be used in Handle methods as they are inside the lock already by default.
	/// </summary>
	protected readonly object ReaderLock = new();

	/// <summary>
	/// The version is equal to the number of messages dispatched to this subscriber's handlers.
	/// The version is incremented after the handler has been processed, inside the
	/// <see cref="ReaderLock"/>, so a reader holding the lock always sees state and version agree.
	/// A message matching more than one of this subscriber's registrations advances the version
	/// once per registration. This can be used to ensure subscriber state for tests.
	/// </summary>
	public int Version { get; private set; }

	protected void Subscribe<T>(IHandle<T> handler) where T : class, IMessage {
		if (_eventSubscriber == null)
			throw new InvalidOperationException("TransientSubscriber not created with EventBus to register on.");
		_subscriptions.Add(_eventSubscriber.Subscribe<T>(new SerializedHandler<T>(this, handler)));
	}

	protected void Subscribe<T>(IHandleCommand<T> handler) where T : Command {
		if (_commandSubscriber == null)
			throw new InvalidOperationException("TransientSubscriber not created with CommandBus to register on.");
		_subscriptions.Add(_commandSubscriber.Subscribe<T>(new SerializedCommandHandler<T>(this, handler)));
	}

	/// <summary>
	/// Every message handled by the subscriber passes through here. Unlike a queued subscriber this
	/// one is dispatched on whichever thread published, so the lock is the only thing serializing
	/// handlers against each other and against readers. Registration order and dispatch order are
	/// untouched — the wrapper calls straight through.
	/// </summary>
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
