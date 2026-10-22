using CourseRegistration.Domain;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;

namespace CourseRegistration.ReadModels;

/// <summary>Sends a reminder to one student.</summary>
public interface IReminderSender {
	/// <summary>Sends the reminder and returns where it went.</summary>
	string Send(Guid studentId, string term, string kind);
}

/// <summary>What a job run did.</summary>
public sealed record JobResult(bool Claimed, int Done);

/// <summary>
/// The add/drop deadline jobs. Every instance runs them on the same schedule; the window claim makes
/// exactly one of them do the work.
/// </summary>
public sealed class AddDropJobs {
	/// <summary>The reminder kind for "add/drop closes soon".</summary>
	public const string ClosingSoon = "add-drop-closing";

	private readonly JobClaims _claims;
	private readonly ReminderLedger _ledger;
	private readonly TimetableQueries _timetables;
	private readonly ICommandPublisher _bus;
	private readonly IReminderSender _sender;
	private readonly string _instance;

	/// <summary>Creates the jobs for one instance, named <paramref name="instance"/> in its claims.</summary>
	public AddDropJobs(JobClaims claims, ReminderLedger ledger, TimetableQueries timetables,
		ICommandPublisher bus, IReminderSender sender, string instance) {
		_claims = claims;
		_ledger = ledger;
		_timetables = timetables;
		_bus = bus;
		_sender = sender;
		_instance = instance;
	}

	/// <summary>Reminds every student with an open schedule in <paramref name="term"/> that add/drop closes soon.</summary>
	/// <remarks>
	/// Each reminder is recorded before it is sent, so a student is never reminded twice, even by a
	/// later window or another instance; a send that fails after recording is not retried. Record
	/// after sending instead where a missed message costs more than a repeated one.
	/// </remarks>
	public JobResult RemindClosing(string term, DateTimeOffset window) {
		if (!_claims.TryClaim($"remind-closing:{term}", window, _instance))
			return new JobResult(false, 0);
		var sent = 0;
		foreach (var schedule in _timetables.OpenSchedules(term)) {
			if (!_ledger.TryRecord(schedule.StudentId, term, ClosingSoon, $"student:{schedule.StudentId}"))
				continue;
			_sender.Send(schedule.StudentId, term, ClosingSoon);
			sent++;
		}
		_claims.Complete($"remind-closing:{term}", window, _instance);
		return new JobResult(true, sent);
	}

	/// <summary>Closes add/drop on every open schedule in <paramref name="term"/>.</summary>
	/// <remarks>Safe to repeat: closing a closed schedule changes nothing.</remarks>
	public JobResult CloseAddDrop(string term, DateTimeOffset window) {
		if (!_claims.TryClaim($"close-add-drop:{term}", window, _instance))
			return new JobResult(false, 0);
		var closed = 0;
		var source = new CorrelationSource(Guid.NewGuid(), Guid.Empty, Guid.NewGuid());
		foreach (var schedule in _timetables.OpenSchedules(term)) {
			var command = MessageBuilder.From(source).Build(() => new CloseAddDrop(schedule.ScheduleId));
			if (_bus.TrySend(command, out _))
				closed++;
		}
		_claims.Complete($"close-add-drop:{term}", window, _instance);
		return new JobResult(true, closed);
	}
}
