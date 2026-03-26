#nullable enable
using System;
using System.Reactive.Disposables;

namespace ReactiveDomain.Messaging.Bus;

/// <summary>
/// A zero-thread <see cref="IDispatcher"/> implementation for test construction.
/// All methods are no-ops. <see cref="Subscribe{T}(IHandle{T}, bool)"/> returns <see cref="Disposable.Empty"/>.
/// <see cref="Send"/> calls <c>command.Succeed()</c> so commands don't hang.
/// </summary>
public class NullBus : IDispatcher {
	public string Name => nameof(NullBus);

	public bool Idle => true;

	public virtual void Publish(IMessage message) { }

	public virtual IDisposable Subscribe<T>(IHandle<T> handler, bool includeDerived = true) where T : class, IMessage
		=> Disposable.Empty;

	public virtual IDisposable SubscribeToAll(IHandle<IMessage> handler)
		=> Disposable.Empty;

	public virtual void Unsubscribe<T>(IHandle<T> handler) where T : class, IMessage { }

	public virtual bool HasSubscriberFor<T>(bool includeDerived = false) where T : class, IMessage
		=> false;

	public virtual void Send(ICommand command, string? exceptionMsg = null, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null)
		=> command.Succeed();

	public virtual bool TrySend(ICommand command, out CommandResponse response, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null) {
		response = command.Succeed();
		return true;
	}

	public virtual bool TrySendAsync(ICommand command, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null) {
		command.Succeed();
		return true;
	}

	public virtual IDisposable Subscribe<T>(IHandleCommand<T> handler) where T : class, ICommand
		=> Disposable.Empty;

	public virtual void Unsubscribe<T>(IHandleCommand<T> handler) where T : class, ICommand { }

	public void Dispose() {
		Dispose(true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing) { }
}
