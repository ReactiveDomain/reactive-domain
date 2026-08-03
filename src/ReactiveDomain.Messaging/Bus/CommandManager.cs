using System.Collections.Concurrent;
using ReactiveDomain.Logging;

namespace ReactiveDomain.Messaging.Bus;

public class CommandManager :
	QueuedSubscriber,
	IHandle<CommandResponse>,
	IHandle<AckCommand>,
	IHandle<AckTimeout>,
	IHandle<CompletionTimeout> {
	private static readonly ILogger _log = LogManager.GetLogger("ReactiveDomain");
	private static readonly TimeSpan _defaultAckTimeout = TimeSpan.FromMilliseconds(100);

	/// <summary>
	/// The response timeout applied to a command that neither the send site nor the owning bus
	/// configured. This is the value the manager has always used; it remains the fallback when
	/// no <c>defaultResponseTimeout</c> is supplied at construction.
	/// </summary>
	public static readonly TimeSpan DefaultResponseTimeout = TimeSpan.FromMilliseconds(500);

	private readonly TimeSpan _defaultResponseTimeout;
	private readonly IBus _outBus;
	private readonly IBus _timeoutBus;
	private readonly ConcurrentDictionary<Guid, CommandTracker> _pendingCommands;
	private bool _disposed;

	/// <param name="bus">The bus commands and responses are published on.</param>
	/// <param name="timeoutBus">The bus timeout messages are scheduled on.</param>
	/// <param name="defaultResponseTimeout">The response timeout used for commands registered
	/// without an explicit one. Defaults to <see cref="DefaultResponseTimeout"/> when unset,
	/// which is the historical behavior. Must be greater than zero when supplied.</param>
	public CommandManager(IBus bus, IBus timeoutBus, TimeSpan? defaultResponseTimeout = null) : base(bus) {
		if (defaultResponseTimeout is { } configured && configured <= TimeSpan.Zero) {
			throw new ArgumentOutOfRangeException(
				nameof(defaultResponseTimeout),
				configured,
				"The default response timeout must be greater than zero.");
		}
		_defaultResponseTimeout = defaultResponseTimeout ?? DefaultResponseTimeout;
		_outBus = bus;
		_timeoutBus = timeoutBus;
		_pendingCommands = new ConcurrentDictionary<Guid, CommandTracker>();
		Subscribe<CommandResponse>(this);
		Subscribe<AckCommand>(this);
	}
	public TaskCompletionSource<CommandResponse> RegisterCommandAsync(
		ICommand command,
		TimeSpan? ackTimeout = null,
		TimeSpan? responseTimeout = null) {
		if (_disposed) {
			throw new ObjectDisposedException(nameof(CommandManager));
		}

		if (_log.LogLevel >= LogLevel.Debug)
			_log.Debug("Registering command tracker for" + command.GetType().Name);
		if (_pendingCommands.ContainsKey(command.MsgId))
			throw new CommandException($"Command tracker already registered for this Command {command.GetType().Name} Id {command.MsgId}.", command);

		var tcs = new TaskCompletionSource<CommandResponse>();
		var tracker = new CommandTracker(
			command,
			tcs,
			() => {
				if (_pendingCommands.TryRemove(command.MsgId, out var tr))
					tr.Dispose();
			},
			() => {
				_outBus.Publish(new Canceled(command));
				if (_pendingCommands.TryRemove(command.MsgId, out var tr))
					tr.Dispose();
			},
			ackTimeout ?? _defaultAckTimeout,
			responseTimeout ?? _defaultResponseTimeout,
			_timeoutBus);
		if (_pendingCommands.TryAdd(command.MsgId, tracker)) {
			return tcs;
		}
		//Add failed, cleanup & throw
		tracker.Dispose();
		tcs.SetResult(new Canceled(command));
		tcs.SetCanceled();
		throw new CommandException($"Failed to register command tracker for this Command {command.GetType().Name} Id {command.MsgId}.", command);

	}

	public void Handle(CommandResponse message) {
		_pendingCommands.TryGetValue(message.CommandId, out var tracker);
		tracker?.Handle(message);
	}

	public void Handle(AckCommand message) {
		_pendingCommands.TryGetValue(message.CommandId, out var tracker);
		tracker?.Handle(message);
	}
	public void Handle(AckTimeout message) {
		_pendingCommands.TryGetValue(message.CommandId, out var tracker);
		tracker?.Handle(message);
	}
	public void Handle(CompletionTimeout message) {
		_pendingCommands.TryGetValue(message.CommandId, out var tracker);
		tracker?.Handle(message);
	}


	protected override void Dispose(bool disposing) {
		//n.b. we want to shut down the queue in the base class before iterating through the trackers
		base.Dispose(disposing);

		if (_disposed)
			return;
		_disposed = true;
		if (!disposing)
			return;

		var trackers = _pendingCommands.Values.ToArray();
		for (var i = 0; i < trackers.Length; i++) {
			trackers[i].Dispose();
		}
	}
}
