namespace ReactiveDomain.Messaging.Bus;

public class MultiQueuedPublisher : ICommandPublisher, IPublisher, IDisposable {
	private readonly CommandManager _manager;
	private readonly IBus _bus;
	private readonly MultiQueuedHandler? _publishQueue;
	private readonly LaterService _laterService;
	private readonly InMemoryBus _timeoutBus;
	public bool Idle => _publishQueue?.Idle ?? true;
	/// <param name="bus">The bus messages are published on.</param>
	/// <param name="queueCount">The number of publish queues; zero publishes on the calling thread.</param>
	/// <param name="slowMsgThreshold">Diagnostic only: a publish-queue message that takes longer than
	/// this is logged as slow. It is not a timeout and has no bearing on when a command fails.</param>
	/// <param name="slowCmdThreshold">Diagnostic only: a command whose round trip exceeds this is logged
	/// as slow. It is not a timeout and has no bearing on when a command fails.</param>
	/// <param name="defaultAckTimeout">The ack timeout for commands sent without an explicit one.
	/// Resolution is per-send, then this, then <see cref="CommandManager.DefaultAckTimeout"/>.</param>
	/// <param name="defaultResponseTimeout">The response timeout for commands sent without an explicit
	/// one — including sends nested inside a command handler, which have no send site to pass one from.
	/// Resolution is per-send, then this, then <see cref="CommandManager.DefaultResponseTimeout"/>.</param>
	public MultiQueuedPublisher(
		IBus bus,
		uint queueCount,
		TimeSpan? slowMsgThreshold,
		TimeSpan? slowCmdThreshold,
		TimeSpan? defaultAckTimeout = null,
		TimeSpan? defaultResponseTimeout = null) {
		// Rejected before anything is allocated, so a bad argument cannot leave a running queue behind.
		CommandManager.EnsurePositive(slowMsgThreshold, nameof(slowMsgThreshold));
		CommandManager.EnsurePositive(slowCmdThreshold, nameof(slowCmdThreshold));
		CommandManager.EnsurePositive(defaultAckTimeout, nameof(defaultAckTimeout));
		CommandManager.EnsurePositive(defaultResponseTimeout, nameof(defaultResponseTimeout));
		_bus = bus;
		_timeoutBus = new InMemoryBus(nameof(_timeoutBus), false);
		_laterService = new LaterService(_timeoutBus, TimeSource.System);
		// ReSharper disable once RedundantTypeArgumentsOfMethod
		_timeoutBus.Subscribe<DelaySendEnvelope>(_laterService);
		_laterService.Start();

		_manager = new CommandManager(bus, _timeoutBus, defaultAckTimeout, defaultResponseTimeout, slowCmdThreshold);
		_timeoutBus.Subscribe<AckTimeout>(_manager);
		_timeoutBus.Subscribe<CompletionTimeout>(_manager);
		if (queueCount > 0) {
			_publishQueue = new MultiQueuedHandler(
				(int)queueCount,
				_ => new QueuedHandler(
					new AdHocHandler<IMessage>(bus.Publish),
					nameof(MultiQueuedPublisher),
					slowMsgThreshold: slowMsgThreshold));
			_publishQueue.Start();
		}
	}
	public void Publish(IMessage message) {
		if (_publishQueue == null) {
			_bus.Publish(message);
		} else {
			_publishQueue.Publish(message);
		}
	}
	public void Send(ICommand command, string? exceptionMsg = null, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null) {
		if (command.IsCanceled) {
			Publish(command.Canceled());
			throw new CommandCanceledException(command);
		}

		Execute(command, out var result, true, responseTimeout, ackTimeout);
		if (result is Success)
			return;

		var fail = result as Fail;
		if (fail?.Exception != null)
			throw new CommandException(exceptionMsg ?? fail.Exception.Message, fail.Exception, command);
		throw new CommandException(exceptionMsg ?? $"{command.GetType().Name}: Failed", command);
	}
	public bool TrySend(ICommand command,
		out CommandResponse response,
		TimeSpan? responseTimeout = null,
		TimeSpan? ackTimeout = null) {
		try {
			if (command.IsCanceled) {
				response = command.Canceled();
				Publish(response);
				return false;
			}
			Execute(command, out response!, true, responseTimeout, ackTimeout);
		} catch (Exception ex) {
			response = command.Fail(ex);
		}
		return response is Success;
	}

	public bool TrySendAsync(ICommand command, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null) {
		try {
			if (command.IsCanceled) {
				var response = command.Canceled();
				Publish(response);
				return false;
			}
			Execute(command, out _, false, responseTimeout, ackTimeout);
		} catch (Exception) {
			return false;
		}
		return true;

	}

	private void Execute(
		ICommand command,
		out CommandResponse? response,
		bool blocking = true,
		TimeSpan? responseTimeout = null,
		TimeSpan? ackTimeout = null) {

		TaskCompletionSource<CommandResponse>? tcs = null;
		try {
			// A null here is the send site declining to choose; the manager resolves it against the
			// bus default and then against RD's documented constant. No diagnostic threshold is
			// consulted along the way.
			tcs = _manager.RegisterCommandAsync(command, ackTimeout, responseTimeout);
		} catch (CommandException ex) {
			tcs?.SetResult(command.Fail(ex));
			throw;
		} catch (Exception ex) {
			tcs?.SetResult(command.Fail(ex));
			throw new CommandException("Error executing command: ", ex, command);
		}
		try {
			//n.b. if this does not throw result will be set asynchronously 
			//in the registered handler in the _manager 

			Publish(command);
		} catch (Exception ex) {
			tcs.SetResult(command.Fail(ex));
			throw;
		}

		if (!blocking) {
			response = null;
			return;
		}
		try {
			//blocking caller until result is set 
			response = tcs.Task.Result;
		} catch (AggregateException aggEx) {
			if (aggEx.InnerException != null) {
				throw aggEx.InnerException;
			}
			throw;
		}
	}

	protected virtual void Dispose(bool disposing) {
		if (disposing) {
			_laterService.Dispose();
			_manager.Dispose();
			_timeoutBus.Dispose();
			_publishQueue?.Stop();//TODO: do we need to flush/empty the queue here?
		}
	}
	public void Dispose() {
		Dispose(true);
		GC.SuppressFinalize(this);
	}
}
