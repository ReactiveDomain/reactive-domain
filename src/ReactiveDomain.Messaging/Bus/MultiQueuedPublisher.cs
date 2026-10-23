namespace ReactiveDomain.Messaging.Bus;

public class MultiQueuedPublisher : ICommandPublisher, IPublisher, IDisposable {
	private readonly CommandManager _manager;
	private readonly IBus _bus;
	private readonly MultiQueuedHandler? _publishQueue;
	private readonly LaterService _laterService;
	private readonly InMemoryBus _timeoutBus;
	// The publisher whose queue thread this is, while that thread hands a message to the bus.
	[ThreadStatic] private static MultiQueuedPublisher? _handlingOn;
	public bool Idle => _publishQueue?.Idle ?? true;
	/// <summary>
	/// Creates a publisher that publishes to <paramref name="bus"/>, and sends commands with the
	/// default timeouts supplied here.
	/// </summary>
	/// <param name="bus">The bus messages are published on.</param>
	/// <param name="queueCount">The number of publish queues; zero publishes on the calling thread. With
	/// queues, handlers run on the publisher's own threads: ambient <see cref="AsyncLocal{T}"/> state set by
	/// the sender does not cross the queue, so anything a handler needs travels on the message. A command a
	/// handler sends from one of these threads with <see cref="Send"/>, <see cref="TrySend"/> or
	/// <see cref="SendAsync"/> runs inline on it rather than queueing behind its sender — so it runs before
	/// anything the handler has already published — while <see cref="TrySendAsync"/> still enqueues, and a
	/// queue-affine command always goes to its own queue. A handler hosted on its own queue (a
	/// <see cref="QueuedSubscriber"/>) is not on one of these threads, and a command it sends to itself
	/// still waits behind it.</param>
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
					new AdHocHandler<IMessage>(HandleFromQueue),
					nameof(MultiQueuedPublisher),
					slowMsgThreshold: slowMsgThreshold));
			_publishQueue.Start();
		}
	}

	private void HandleFromQueue(IMessage message) {
		_handlingOn = this;
		try {
			_bus.Publish(message);
		} finally {
			_handlingOn = null;
		}
	}

	/// <inheritdoc cref="CommandManager.AckTimeout"/>
	public TimeSpan AckTimeout {
		get => _manager.AckTimeout;
		set => _manager.AckTimeout = value;
	}

	/// <inheritdoc cref="CommandManager.ResponseTimeout"/>
	public TimeSpan ResponseTimeout {
		get => _manager.ResponseTimeout;
		set => _manager.ResponseTimeout = value;
	}

	/// <inheritdoc cref="CommandManager.SlowCommandThreshold"/>
	public TimeSpan SlowCommandThreshold {
		get => _manager.SlowCommandThreshold;
		set => _manager.SlowCommandThreshold = value;
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

	/// <inheritdoc cref="ICommandPublisher.SendAsync"/>
	/// <remarks>
	/// With queues the caller is released as soon as the command is enqueued and resumes when the response
	/// arrives — except on one of the publisher's own queue threads, where the handler runs inline before
	/// this returns, so a handler that blocks on the task cannot wait behind itself. To fire and forget from
	/// a handler, use <see cref="TrySendAsync"/>, which always enqueues. With <c>queueCount == 0</c> the
	/// handler runs on the calling thread before this returns.
	/// </remarks>
	public Task<CommandResponse> SendAsync(ICommand command, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null) {
		TaskCompletionSource<CommandResponse> tcs;
		try {
			if (command.IsCanceled) {
				var response = command.Canceled();
				Publish(response);
				return Task.FromResult(response);
			}
			tcs = Dispatch(command, responseTimeout, ackTimeout, inline: true);
		} catch (Exception ex) {
			return Task.FromResult(command.Fail(ex));
		}
		return AwaitResponse(command, tcs.Task);
	}

	/// <summary>Maps a faulted or canceled tracker task to the <see cref="Fail"/> that <see cref="TrySend"/> would return.</summary>
	private static async Task<CommandResponse> AwaitResponse(ICommand command, Task<CommandResponse> response) {
		try {
			return await response.ConfigureAwait(false);
		} catch (Exception ex) {
			return command.Fail(ex);
		}
	}

	private void Execute(
		ICommand command,
		out CommandResponse? response,
		bool blocking = true,
		TimeSpan? responseTimeout = null,
		TimeSpan? ackTimeout = null) {
		var tcs = Dispatch(command, responseTimeout, ackTimeout, inline: blocking);
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

	/// <summary>Registers the command's tracker and publishes the command; the returned source completes with the response.</summary>
	/// <param name="inline">Whether the send may run the handler inline on this thread, where this is one of the publisher's own queue threads: true for the sends whose caller consumes the response, false for fire-and-forget.</param>
	private TaskCompletionSource<CommandResponse> Dispatch(
		ICommand command,
		TimeSpan? responseTimeout,
		TimeSpan? ackTimeout,
		bool inline) {

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

			// On one of this publisher's own queue threads the sender is a handler, and a response is
			// consumed by blocking that thread: queued, the command could land behind its own sender
			// and only time out. So response-bearing sends run inline there — and only those: a
			// fire-and-forget TrySendAsync keeps its enqueue-and-return contract (running the handler
			// before returning would add the child's whole duration to the sender's round trip), and
			// a queue-affine command always goes through the queues that serialize its key, even
			// though a blocking nested send of one can then wait behind its own sender.
			if (inline && ReferenceEquals(_handlingOn, this) && command is not IQueueAffineMessage)
				_bus.Publish(command);
			else
				Publish(command);
		} catch (Exception ex) {
			tcs.SetResult(command.Fail(ex));
			throw;
		}
		return tcs;
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
