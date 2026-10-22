using CourseRegistration.Domain;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;

namespace CourseRegistration.ReadModels;

/// <summary>Sends a reminder to one student.</summary>
public interface IReminderSender {
	/// <summary>Sends the reminder and returns where it went.</summary>
	string Send(Guid studentId, string term, string kind);
}

/// <summary>What a job run did. A run with steps left unsent is left unfinished, so a later run takes it over.</summary>
public sealed record JobResult(bool Claimed, int Done, int Unsent = 0);

/// <summary>
/// The add/drop deadline jobs. Every instance runs them on the same schedule; the window claim makes
/// one of them do the work.
/// </summary>
/// <remarks>
/// A window is marked complete only when every step in it succeeded. Otherwise its claim lapses after
/// the lease and the next run, on any instance, repeats it; each step is idempotent, so the repeat
/// does only what is left.
/// </remarks>
public sealed class AddDropJobs {
	/// <summary>The reminder kind for "add/drop closes soon".</summary>
	public const string ClosingSoon = "add-drop-closing";

	private readonly JobClaims _claims;
	private readonly ReminderLedger _ledger;
	private readonly TimetableQueries _timetables;
	private readonly ICommandPublisher _bus;
	private readonly IReminderSender _sender;
	private readonly string _instance;
	private readonly TimeSpan _lease;

	/// <summary>Creates the jobs for one instance, named <paramref name="instance"/> in its claims.</summary>
	/// <param name="lease">How long a claim is held before another instance may take it over; 10 minutes when null.</param>
	public AddDropJobs(JobClaims claims, ReminderLedger ledger, TimetableQueries timetables,
		ICommandPublisher bus, IReminderSender sender, string instance, TimeSpan? lease = null) {
		_claims = claims;
		_ledger = ledger;
		_timetables = timetables;
		_bus = bus;
		_sender = sender;
		_instance = instance;
		_lease = lease ?? TimeSpan.FromMinutes(10);
	}

	/// <summary>Reminds every student with an open schedule in <paramref name="term"/> that add/drop closes soon.</summary>
	/// <remarks>
	/// Each reminder is recorded before it is sent, so a student is never reminded twice, even by a
	/// later window or another instance; a send that throws after recording is not retried, and leaves
	/// the window unfinished for a later run to remind the rest. Record after sending instead where a
	/// missed message costs more than a repeated one.
	/// </remarks>
	public JobResult RemindClosing(string term, DateTimeOffset window) {
		if (!_claims.TryClaim($"remind-closing:{term}", window, _instance, _lease))
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
		if (!_claims.TryClaim($"close-add-drop:{term}", window, _instance, _lease))
			return new JobResult(false, 0);
		int closed = 0, unsent = 0;
		var source = new CorrelationSource(Guid.NewGuid(), Guid.Empty, Guid.NewGuid());
		foreach (var schedule in _timetables.OpenSchedules(term)) {
			var command = MessageBuilder.From(source).Build(() => new CloseAddDrop(schedule.ScheduleId));
			if (_bus.TrySend(command, out _))
				closed++;
			else
				unsent++;
		}
		if (unsent == 0)
			_claims.Complete($"close-add-drop:{term}", window, _instance);
		return new JobResult(true, closed, unsent);
	}
}
