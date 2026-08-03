using System.Diagnostics;
using ReactiveDomain.Logging;

namespace ReactiveDomain.Messaging.Bus;

public class CommandTracker : IDisposable {
	private static readonly ILogger _log = LogManager.GetLogger("ReactiveDomain");
	private readonly ICommand _command;
	private readonly TaskCompletionSource<CommandResponse> _tcs;
	// ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
	private readonly IPublisher _bus;
	private readonly Action _completionAction;
	private readonly Action _cancelAction;
	private readonly TimeSpan _slowCmdThreshold;
	private readonly long _startedTimestamp = Stopwatch.GetTimestamp();
	private bool _disposed;

	private const long PendingAck = 0;
	private const long PendingResponse = 1;
	private const long Complete = 2;
	private long _state;


	/// <param name="command">The command being tracked.</param>
	/// <param name="tcs">Completed with the command's response, or faulted with its failure.</param>
	/// <param name="completionAction">Run once the command completes.</param>
	/// <param name="cancelAction">Run once the command is canceled or times out.</param>
	/// <param name="ackTimeout">How long to wait for a handler to acknowledge the command before
	/// failing it. A timeout, not a diagnostic threshold.</param>
	/// <param name="completionTimeout">How long to wait for the handler to complete before failing the
	/// command. A timeout, not a diagnostic threshold.</param>
	/// <param name="bus">The bus the timeout envelopes are scheduled on.</param>
	/// <param name="slowCmdThreshold">Diagnostic only: a command that completes later than this is
	/// logged as slow. It never fails, cancels or delays the command. Defaults to
	/// <see cref="CommandManager.DefaultSlowCommandThreshold"/>.</param>
	public CommandTracker(
		ICommand command,
		TaskCompletionSource<CommandResponse> tcs,
		Action completionAction,
		Action cancelAction,
		TimeSpan ackTimeout,
		TimeSpan completionTimeout,
		IPublisher bus,
		TimeSpan? slowCmdThreshold = null) {

		_command = command;
		_tcs = tcs;
		_bus = bus;
		_completionAction = completionAction;
		_cancelAction = cancelAction;
		_slowCmdThreshold = slowCmdThreshold ?? CommandManager.DefaultSlowCommandThreshold;
		_state = PendingAck;
		_bus.Publish(new DelaySendEnvelope(TimeSource.System, ackTimeout, new AckTimeout(_command.MsgId)));
		_bus.Publish(new DelaySendEnvelope(TimeSource.System, completionTimeout, new CompletionTimeout(_command.MsgId)));
	}

	public void Handle(CommandResponse message) {
		Interlocked.Exchange(ref _state, Complete);
		if (_tcs.TrySetResult(message)) {
			LogIfSlow();
			_completionAction();
		}
	}

	/// <summary>
	/// The slow-command diagnostic the slow-command threshold is named for: a completed command that
	/// took longer than the threshold is logged, and that is the threshold's only effect.
	/// </summary>
	private void LogIfSlow() {
		if (_log.LogLevel < LogLevel.Trace)
			return;
		var elapsed = Stopwatch.GetElapsedTime(_startedTimestamp);
		if (elapsed > _slowCmdThreshold)
			_log.Trace("SLOW COMMAND [{0}]: {1}ms.", _command.GetType().Name, (int)elapsed.TotalMilliseconds);
	}

	private long _ackCount;
	public void Handle(AckCommand message) {
		Interlocked.Increment(ref _ackCount);
		var curState = Interlocked.Read(ref _state);
		if (curState != PendingAck || Interlocked.CompareExchange(ref _state, PendingResponse, curState) != curState) {
			if (_log.LogLevel >= LogLevel.Error)
				_log.Error(_command.GetType().Name + " Multiple Handlers Acked Command");
			if (_tcs.TrySetException(new CommandOversubscribedException(" multiple handlers responded to the command", _command)))
				_cancelAction();
		}
	}

	public void Handle(AckTimeout message) {
		if (Interlocked.Read(ref _state) == PendingAck) {
			if (_tcs.TrySetException(new CommandNotHandledException(" timed out waiting for a handler to start. Make sure a command handler is subscribed", _command))) {
				if (_log.LogLevel >= LogLevel.Error)
					_log.Error(_command.GetType().Name + " command not handled (no handler)");
				_cancelAction();
			}
		}
	}

	public void Handle(CompletionTimeout message) {
		if (Interlocked.Read(ref _state) == PendingResponse) {
			if (_tcs.TrySetException(new CommandTimedOutException(" timed out waiting for handler to complete.", _command))) {
				if (_log.LogLevel >= LogLevel.Error)
					_log.Error(_command.GetType().Name + " command timed out");
				_cancelAction();
			}
		}
	}

	public void Dispose() {
		Dispose(true);
		GC.SuppressFinalize(this);

	}

	public void Dispose(bool disposing) {
		if (_disposed)
			return;

		if (disposing) {
			if (!_tcs.Task.IsCanceled && !_tcs.Task.IsCompleted && !_tcs.Task.IsFaulted) {
				_tcs.TrySetCanceled();
			}
			_tcs.Task.Dispose();
		}
		_disposed = true;
	}
}

public record AckTimeout(Guid CommandId) : IMessage {
	public Guid MsgId { get; private set; } = Guid.NewGuid();
}

public record CompletionTimeout(Guid CommandId) : IMessage {
	public Guid MsgId { get; private set; } = Guid.NewGuid();
}
