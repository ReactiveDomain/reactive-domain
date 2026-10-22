using Dapper;
using Newtonsoft.Json;
using Npgsql;
using ReactiveDomain.Messaging;

namespace CourseRegistration.ReadModels;

// Tables written directly, not projected from events. Each is idempotent by its natural key, so a
// retried job or a restarted instance repeats nothing.

/// <summary>Which reminders have gone to which students. A reminder goes once per student, term and kind.</summary>
public sealed class ReminderLedger {
	private readonly NpgsqlDataSource _dataSource;

	/// <summary>Writes through <paramref name="dataSource"/>.</summary>
	public ReminderLedger(NpgsqlDataSource dataSource) => _dataSource = dataSource;

	/// <summary>Records the reminder unless it was already sent.</summary>
	/// <returns>True when this call recorded it, so the caller sends it; false when it went before.</returns>
	/// <remarks>
	/// One statement, not a check followed by an insert: two instances checking at once would both see
	/// nothing and both send.
	/// </remarks>
	public bool TryRecord(Guid studentId, string term, string kind, string sentTo) {
		using var connection = _dataSource.OpenConnection();
		return connection.Execute("""
			INSERT INTO reminder_sends (student_id, term, kind, sent_to, sent_at)
			VALUES (@studentId, @term, @kind, @sentTo, now())
			ON CONFLICT (student_id, term, kind) DO NOTHING
			""", new { studentId, term, kind, sentTo }) == 1;
	}

	/// <summary>True when the reminder has been recorded.</summary>
	public bool WasSent(Guid studentId, string term, string kind) {
		using var connection = _dataSource.OpenConnection();
		return connection.ExecuteScalar<bool>("""
			SELECT EXISTS (SELECT 1 FROM reminder_sends WHERE student_id = @studentId AND term = @term AND kind = @kind)
			""", new { studentId, term, kind });
	}
}

/// <summary>A command as the audit log recorded it.</summary>
public sealed record AuditedCommand(Guid MsgId, Guid CorrelationId, Guid CausationId, string CommandType, string Body, DateTime RecordedAt);

/// <summary>Every command sent, with its correlation, so one registration can be traced end to end.</summary>
public sealed class CommandAuditLog {
	private static readonly JsonSerializerSettings BodySettings = new() { NullValueHandling = NullValueHandling.Ignore };
	private readonly NpgsqlDataSource _dataSource;

	/// <summary>Writes through <paramref name="dataSource"/>.</summary>
	public CommandAuditLog(NpgsqlDataSource dataSource) => _dataSource = dataSource;

	/// <summary>Records <paramref name="command"/>.</summary>
	/// <returns>False when a command with its MsgId was already recorded; a resend is not a second command.</returns>
	public bool Record(ICommand command) {
		using var connection = _dataSource.OpenConnection();
		return connection.Execute("""
			INSERT INTO command_audit (msg_id, correlation_id, causation_id, command_type, body, recorded_at)
			VALUES (@MsgId, @CorrelationId, @CausationId, @CommandType, @Body::jsonb, clock_timestamp())
			ON CONFLICT (msg_id) DO NOTHING
			""", new {
			command.MsgId,
			command.CorrelationId,
			command.CausationId,
			CommandType = command.GetType().Name,
			Body = JsonConvert.SerializeObject(command, BodySettings)
		}) == 1;
	}

	/// <summary>Every command in one correlation, in the order recorded.</summary>
	public IReadOnlyList<AuditedCommand> Trace(Guid correlationId) {
		using var connection = _dataSource.OpenConnection();
		return connection.Query<AuditedCommand>("""
			SELECT msg_id AS MsgId, correlation_id AS CorrelationId, causation_id AS CausationId,
			       command_type AS CommandType, body::text AS Body, recorded_at AS RecordedAt
			FROM command_audit WHERE correlation_id = @correlationId ORDER BY recorded_at, msg_id
			""", new { correlationId }).AsList();
	}
}

/// <summary>One claimed run of a job.</summary>
public sealed record JobRun(string ClaimedBy, bool Completed);

/// <summary>
/// Runs of scheduled jobs. One instance holds each window at a time, however many try; a claim that
/// is not completed within its lease can be taken over, so a crashed instance does not lose the window.
/// </summary>
public sealed class JobClaims {
	private readonly NpgsqlDataSource _dataSource;

	/// <summary>Writes through <paramref name="dataSource"/>.</summary>
	public JobClaims(NpgsqlDataSource dataSource) => _dataSource = dataSource;

	/// <summary>Claims <paramref name="job"/>'s run for <paramref name="windowStart"/> for <paramref name="lease"/>.</summary>
	/// <returns>
	/// True for the one caller that now holds the window. False while another holds an unexpired lease
	/// on it, and once it is completed.
	/// </returns>
	/// <remarks>
	/// One statement decides: the insert that lands first owns the window, and a takeover updates the row
	/// only while its lease has lapsed, which Postgres re-checks under the row lock, so two takers cannot
	/// both win. A lease should outlast a normal run; a run that takes longer can be repeated by a taker,
	/// so the job's steps must be idempotent.
	/// </remarks>
	public bool TryClaim(string job, DateTimeOffset windowStart, string instance, TimeSpan lease) {
		using var connection = _dataSource.OpenConnection();
		return connection.Execute("""
			INSERT INTO job_runs (job_name, window_start, claimed_by, claimed_at, lease_until)
			VALUES (@job, @windowStart, @instance, clock_timestamp(), clock_timestamp() + @lease)
			ON CONFLICT (job_name, window_start) DO UPDATE SET
				claimed_by = EXCLUDED.claimed_by,
				claimed_at = EXCLUDED.claimed_at,
				lease_until = EXCLUDED.lease_until
			WHERE job_runs.completed_at IS NULL AND job_runs.lease_until < clock_timestamp()
			""", new { job, windowStart, instance, lease }) == 1;
	}

	/// <summary>Marks the run finished. Only the instance holding the claim can.</summary>
	/// <returns>False when <paramref name="instance"/> does not hold the claim, including when it was taken over.</returns>
	public bool Complete(string job, DateTimeOffset windowStart, string instance) {
		using var connection = _dataSource.OpenConnection();
		return connection.Execute("""
			UPDATE job_runs SET completed_at = now()
			WHERE job_name = @job AND window_start = @windowStart AND claimed_by = @instance AND completed_at IS NULL
			""", new { job, windowStart, instance }) == 1;
	}

	/// <summary>Who claimed the window, and whether it finished; null when nobody has.</summary>
	public JobRun? Run(string job, DateTimeOffset windowStart) {
		using var connection = _dataSource.OpenConnection();
		return connection.QuerySingleOrDefault<JobRun>("""
			SELECT claimed_by AS ClaimedBy, completed_at IS NOT NULL AS Completed
			FROM job_runs WHERE job_name = @job AND window_start = @windowStart
			""", new { job, windowStart });
	}
}
