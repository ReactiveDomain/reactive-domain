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

	/// <summary>
	/// How long a command waits for a handler to acknowledge it before it fails with
	/// <see cref="CommandNotHandledException"/>, when neither the send site nor the owning bus supplied
	/// a value. This is a timeout: exceeding it fails the command.
	/// </summary>
	public static readonly TimeSpan DefaultAckTimeout = TimeSpan.FromMilliseconds(100);

	/// <summary>
	/// How long a command waits for its handler to complete before it fails with
	/// <see cref="CommandTimedOutException"/>, when neither the send site nor the owning bus supplied
	/// a value. This is a timeout: exceeding it fails the command.
	/// </summary>
	public static readonly TimeSpan DefaultResponseTimeout = TimeSpan.FromMilliseconds(500);

	/// <summary>
	/// How long a command may take before its completion is logged as slow, when the owning bus supplied
	/// no value. This is a diagnostic threshold: exceeding it logs, and nothing else. It never fails,
	/// cancels or delays a command — <see cref="DefaultResponseTimeout"/> is what does that.
	/// </summary>
	public static readonly TimeSpan DefaultSlowCommandThreshold = TimeSpan.FromMilliseconds(500);

	private readonly TimeSpan _defaultAckTimeout;
	private readonly TimeSpan _defaultResponseTimeout;
	private readonly TimeSpan _slowCmdThreshold;
	private readonly IBus _outBus;
	private readonly IBus _timeoutBus;
	private readonly ConcurrentDictionary<Guid, CommandTracker> _pendingCommands;
	private bool _disposed;

	/// <param name="bus">The bus commands and responses are published on.</param>
	/// <param name="timeoutBus">The bus timeout messages are scheduled on.</param>
	/// <param name="defaultAckTimeout">The ack timeout for commands registered without an explicit one.
	/// Defaults to <see cref="DefaultAckTimeout"/>. Must be greater than zero when supplied.</param>
	/// <param name="defaultResponseTimeout">The response timeout for commands registered without an
	/// explicit one. Defaults to <see cref="DefaultResponseTimeout"/>. Must be greater than zero when
	/// supplied.</param>
	/// <param name="slowCmdThreshold">Diagnostic only: a command whose round trip exceeds this is logged
	/// as slow. It has no bearing on when a command times out. Defaults to
	/// <see cref="DefaultSlowCommandThreshold"/>. Must be greater than zero when supplied.</param>
	public CommandManager(
		IBus bus,
		IBus timeoutBus,
		TimeSpan? defaultAckTimeout = null,
		TimeSpan? defaultResponseTimeout = null,
		TimeSpan? slowCmdThreshold = null) : base(bus) {
		EnsurePositive(defaultAckTimeout, nameof(defaultAckTimeout));
		EnsurePositive(defaultResponseTimeout, nameof(defaultResponseTimeout));
		EnsurePositive(slowCmdThreshold, nameof(slowCmdThreshold));
		_defaultAckTimeout = defaultAckTimeout ?? DefaultAckTimeout;
		_defaultResponseTimeout = defaultResponseTimeout ?? DefaultResponseTimeout;
		_slowCmdThreshold = slowCmdThreshold ?? DefaultSlowCommandThreshold;
		_outBus = bus;
		_timeoutBus = timeoutBus;
		_pendingCommands = new ConcurrentDictionary<Guid, CommandTracker>();
		Subscribe<CommandResponse>(this);
		Subscribe<AckCommand>(this);
	}

	/// <summary>
	/// Rejects a non-positive timeout or threshold at construction, rather than letting it silently
	/// expire every command it is given.
	/// </summary>
	internal static void EnsurePositive(TimeSpan? value, string paramName) {
		if (value is { } supplied && supplied <= TimeSpan.Zero) {
			throw new ArgumentOutOfRangeException(paramName, supplied, $"{paramName} must be greater than zero.");
		}
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
			_timeoutBus,
			_slowCmdThreshold);
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
