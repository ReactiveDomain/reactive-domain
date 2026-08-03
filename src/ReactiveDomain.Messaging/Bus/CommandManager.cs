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
	/// <para>Deliberately under <see cref="DefaultResponseTimeout"/>: only a command that <i>completed</i>
	/// is ever logged as slow, so setting this at or above the response timeout in use leaves nothing
	/// able to reach it and turns the diagnostic off.</para>
	/// </summary>
	public static readonly TimeSpan DefaultSlowCommandThreshold = TimeSpan.FromMilliseconds(400);

	private long _ackTimeoutTicks;
	private long _responseTimeoutTicks;
	private long _slowCmdThresholdTicks;

	/// <summary>
	/// The ack timeout applied to commands registered without an explicit one. Settable at any time.
	/// Must be greater than zero.
	/// <para>Changing this is not a mechanism for adjusting commands already in flight; it applies to
	/// commands registered after it.</para>
	/// </summary>
	public TimeSpan AckTimeout {
		get => new(Interlocked.Read(ref _ackTimeoutTicks));
		set {
			EnsurePositive(value, nameof(AckTimeout));
			Interlocked.Exchange(ref _ackTimeoutTicks, value.Ticks);
		}
	}

	/// <summary>
	/// The response timeout applied to commands registered without an explicit one. Settable at any
	/// time. Must be greater than zero.
	/// <para>Changing this is not a mechanism for adjusting commands already in flight; it applies to
	/// commands registered after it.</para>
	/// </summary>
	public TimeSpan ResponseTimeout {
		get => new(Interlocked.Read(ref _responseTimeoutTicks));
		set {
			EnsurePositive(value, nameof(ResponseTimeout));
			Interlocked.Exchange(ref _responseTimeoutTicks, value.Ticks);
		}
	}

	/// <summary>
	/// Diagnostic only: a command whose round trip exceeds this is logged as slow. Settable at any
	/// time; like the timeouts it applies to commands registered after the change. Must be greater
	/// than zero.
	/// </summary>
	public TimeSpan SlowCommandThreshold {
		get => new(Interlocked.Read(ref _slowCmdThresholdTicks));
		set {
			EnsurePositive(value, nameof(SlowCommandThreshold));
			Interlocked.Exchange(ref _slowCmdThresholdTicks, value.Ticks);
		}
	}
	private readonly IBus _outBus;
	private readonly IBus _timeoutBus;
	private readonly ConcurrentDictionary<Guid, CommandTracker> _pendingCommands;
	private bool _disposed;

	/// <summary>
	/// Creates a manager that tracks the commands in flight on <paramref name="bus"/> and expires them
	/// against the timeouts supplied here.
	/// </summary>
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
		_ackTimeoutTicks = (defaultAckTimeout ?? DefaultAckTimeout).Ticks;
		_responseTimeoutTicks = (defaultResponseTimeout ?? DefaultResponseTimeout).Ticks;
		_slowCmdThresholdTicks = (slowCmdThreshold ?? DefaultSlowCommandThreshold).Ticks;
		_outBus = bus;
		_timeoutBus = timeoutBus;
		_pendingCommands = new ConcurrentDictionary<Guid, CommandTracker>();
		Subscribe<CommandResponse>(this);
		Subscribe<AckCommand>(this);
	}

	/// <summary>Throws if <paramref name="value"/> was supplied and is not greater than zero.</summary>
	/// <remarks>A non-positive value would silently expire every command it was given.</remarks>
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
			ackTimeout ?? AckTimeout,
			responseTimeout ?? ResponseTimeout,
			_timeoutBus,
			SlowCommandThreshold);
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
