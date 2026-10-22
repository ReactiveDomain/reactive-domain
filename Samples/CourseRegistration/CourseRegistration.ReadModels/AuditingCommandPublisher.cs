using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;

namespace CourseRegistration.ReadModels;

/// <summary>Records every command in a <see cref="CommandAuditLog"/> before sending it on.</summary>
/// <remarks>Recorded before the send, so a command that fails is still in the trace.</remarks>
public sealed class AuditingCommandPublisher : ICommandPublisher {
	private readonly ICommandPublisher _inner;
	private readonly CommandAuditLog _log;

	/// <summary>Records to <paramref name="log"/>, then sends through <paramref name="inner"/>.</summary>
	public AuditingCommandPublisher(ICommandPublisher inner, CommandAuditLog log) {
		_inner = inner;
		_log = log;
	}

	/// <inheritdoc/>
	public void Send(ICommand command, string? exceptionMsg = null, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null) {
		_log.Record(command);
		_inner.Send(command, exceptionMsg, responseTimeout, ackTimeout);
	}

	/// <inheritdoc/>
	public bool TrySend(ICommand command, out CommandResponse response, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null) {
		_log.Record(command);
		return _inner.TrySend(command, out response, responseTimeout, ackTimeout);
	}

	/// <inheritdoc/>
	public bool TrySendAsync(ICommand command, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null) {
		_log.Record(command);
		return _inner.TrySendAsync(command, responseTimeout, ackTimeout);
	}
}
