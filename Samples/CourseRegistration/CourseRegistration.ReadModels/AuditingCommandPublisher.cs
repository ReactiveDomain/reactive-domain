using ReactiveDomain.Logging;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;

namespace CourseRegistration.ReadModels;

/// <summary>Records every command in a <see cref="CommandAuditLog"/> before sending it on.</summary>
/// <remarks>
/// Recorded before the send, so a command that fails is still in the trace. A failed recording is
/// logged and the command is sent anyway: the audit lives in the read-side database, and its outage
/// must not stop the write side.
/// </remarks>
public sealed class AuditingCommandPublisher : ICommandPublisher {
	private static readonly ILogger Log = LogManager.GetLogger(typeof(AuditingCommandPublisher).FullName!);

	private readonly ICommandPublisher _inner;
	private readonly CommandAuditLog _log;

	/// <summary>Records to <paramref name="log"/>, then sends through <paramref name="inner"/>.</summary>
	public AuditingCommandPublisher(ICommandPublisher inner, CommandAuditLog log) {
		_inner = inner;
		_log = log;
	}

	/// <inheritdoc/>
	public void Send(ICommand command, string? exceptionMsg = null, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null) {
		Record(command);
		_inner.Send(command, exceptionMsg, responseTimeout, ackTimeout);
	}

	/// <inheritdoc/>
	public bool TrySend(ICommand command, out CommandResponse response, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null) {
		Record(command);
		return _inner.TrySend(command, out response, responseTimeout, ackTimeout);
	}

	/// <inheritdoc/>
	public bool TrySendAsync(ICommand command, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null) {
		Record(command);
		return _inner.TrySendAsync(command, responseTimeout, ackTimeout);
	}

	private void Record(ICommand command) {
		try {
			_log.Record(command);
		} catch (Exception ex) {
			Log.ErrorException(ex, "{0} {1} was not audited", command.GetType().Name, command.MsgId);
		}
	}
}
