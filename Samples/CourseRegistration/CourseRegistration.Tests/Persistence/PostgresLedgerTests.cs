using CourseRegistration.Domain;
using CourseRegistration.ReadModels;
using CourseRegistration.ReadModels.Persistence;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;

namespace CourseRegistration.Tests.Persistence;

/// <summary>The tables written directly rather than projected. They rely on Postgres keys, so they run only there.</summary>
[Collection(PostgresCollection.Name)]
public sealed class PostgresLedgerTests(PostgresFixture postgres) {
	[Fact]
	public void a_reminder_is_recorded_once_however_often_it_is_tried() {
		var ledger = new ReminderLedger(postgres.NewDatabase());
		var student = Guid.NewGuid();

		Assert.False(ledger.WasSent(student, "2026FA", "closing"));
		Assert.True(ledger.TryRecord(student, "2026FA", "closing", "a@example.edu"));
		Assert.False(ledger.TryRecord(student, "2026FA", "closing", "a@example.edu"));
		Assert.True(ledger.TryRecord(student, "2027SP", "closing", "a@example.edu"));
		Assert.True(ledger.WasSent(student, "2026FA", "closing"));
	}

	[Fact]
	public async Task racing_recorders_send_one_reminder() {
		var ledger = new ReminderLedger(postgres.NewDatabase());
		var student = Guid.NewGuid();

		var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => ledger.TryRecord(student, "2026FA", "closing", "a@example.edu"))));

		Assert.Single(results, r => r);
	}

	[Fact]
	public void the_audit_log_traces_a_registration_through_every_command_it_caused() {
		var log = new CommandAuditLog(postgres.NewDatabase());
		using var h = new Harness(withProcess: false);
		var audited = new AuditingCommandPublisher(h.Bus, log);
		h.StartProcess(audited);
		var chem = h.Offer();
		var schedule = h.Open();
		var request = MessageBuilder.New(() => new RequestCourse(schedule, chem));

		audited.Send(request);
		h.Settle();

		var trace = log.Trace(request.CorrelationId);
		Assert.Equal([nameof(RequestCourse), nameof(AddReservedCourse)], trace.Select(c => c.CommandType));
		Assert.Equal(request.MsgId, trace[0].MsgId);
		Assert.NotEqual(request.MsgId, trace[1].CausationId); // caused by the SeatReserved event, not the request
		Assert.Contains($"\"SectionId\":\"{chem}\"", trace[0].Body.Replace(" ", ""));
		Assert.False(log.Record(request));
	}

	[Fact]
	public async Task one_instance_claims_each_window_however_many_try() {
		var claims = new JobClaims(postgres.NewDatabase());
		var window = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);

		var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => Task.Run(() => (i, claims.TryClaim("job", window, $"instance-{i}", TimeSpan.FromHours(1))))));

		var (winner, _) = Assert.Single(results, r => r.Item2);
		Assert.Equal(new JobRun($"instance-{winner}", false), claims.Run("job", window));
		Assert.True(claims.TryClaim("job", window.AddDays(1), "instance-0", TimeSpan.FromHours(1)));
		Assert.False(claims.Complete("job", window, $"instance-{(winner + 1) % 8}"));
		Assert.True(claims.Complete("job", window, $"instance-{winner}"));
		Assert.True(claims.Run("job", window)!.Completed);
		Assert.Null(claims.Run("job", window.AddDays(2)));
	}

	[Fact]
	public void an_unfinished_claim_is_taken_over_once_its_lease_lapses_and_a_finished_one_never() {
		var claims = new JobClaims(postgres.NewDatabase());
		var window = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
		Assert.True(claims.TryClaim("held", window, "a", TimeSpan.FromHours(1)));
		Assert.True(claims.TryClaim("lapsed", window, "a", TimeSpan.Zero));
		Assert.True(claims.TryClaim("done", window, "a", TimeSpan.Zero));
		Assert.True(claims.Complete("done", window, "a"));

		Assert.False(claims.TryClaim("held", window, "b", TimeSpan.FromHours(1)));
		Assert.True(claims.TryClaim("lapsed", window, "b", TimeSpan.FromHours(1)));
		Assert.False(claims.TryClaim("done", window, "b", TimeSpan.FromHours(1)));

		Assert.Equal(new JobRun("b", false), claims.Run("lapsed", window));
		Assert.False(claims.Complete("lapsed", window, "a")); // the crashed instance no longer holds it
		Assert.True(claims.Complete("lapsed", window, "b"));
	}

	[Fact]
	public async Task racing_takers_of_a_lapsed_claim_produce_one_holder() {
		var claims = new JobClaims(postgres.NewDatabase());
		var window = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
		claims.TryClaim("job", window, "crashed", TimeSpan.Zero);

		var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(i =>
			Task.Run(() => claims.TryClaim("job", window, $"taker-{i}", TimeSpan.FromHours(1)))));

		Assert.Single(results, r => r);
	}

	/// <summary>A close that fails leaves the window unfinished; the next run, after the lease, closes what is left.</summary>
	[Fact]
	public void a_close_job_with_failures_stays_unfinished_until_a_later_run_completes_it() {
		var data = postgres.NewDatabase();
		using var h = new Harness(new PostgresProjectionDb(data));
		var timetable = h.StartTimetable();
		var schedules = new[] { h.Open(), h.Open(), h.Open() };
		h.CatchUp(timetable);
		var window = new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero);
		var failOne = new FailFirstBus(h.Bus);
		var claims = new JobClaims(data);

		var first = new AddDropJobs(claims, new ReminderLedger(data), h.Timetables(), failOne, new RecordingSender(), "a", TimeSpan.Zero)
			.CloseAddDrop("2026FA", window);
		h.CatchUp(timetable);
		var second = new AddDropJobs(claims, new ReminderLedger(data), h.Timetables(), h.Bus, new RecordingSender(), "b", TimeSpan.Zero)
			.CloseAddDrop("2026FA", window);

		Assert.Equal((true, 2, 1), (first.Claimed, first.Done, first.Failed));
		Assert.Equal((true, 1, 0), (second.Claimed, second.Done, second.Failed));
		Assert.Equal(new JobRun("b", true), claims.Run("close-add-drop:2026FA", window));
		Assert.All(schedules, s => Assert.True(h.Schedule(s).IsClosed));
	}

	[Fact]
	public void the_deadline_jobs_remind_each_student_once_and_close_each_schedule_once() {
		var data = postgres.NewDatabase();
		using var h = new Harness(new PostgresProjectionDb(data));
		var timetable = h.StartTimetable();
		var open = new[] { h.Open(), h.Open() };
		var otherTerm = h.Open(term: "2027SP");
		h.CatchUp(timetable);
		var sender = new RecordingSender();
		AddDropJobs Instance(string name) =>
			new(new JobClaims(data), new ReminderLedger(data), h.Timetables(), h.Bus, sender, name, TimeSpan.FromHours(1));
		var window = new DateTimeOffset(2026, 9, 10, 0, 0, 0, TimeSpan.Zero);

		var a = Instance("a").RemindClosing("2026FA", window);
		var b = Instance("b").RemindClosing("2026FA", window);
		var nextDay = Instance("b").RemindClosing("2026FA", window.AddDays(1));

		Assert.Equal((true, 2), (a.Claimed, a.Done));
		Assert.Equal((false, 0), (b.Claimed, b.Done));
		Assert.Equal((true, 0), (nextDay.Claimed, nextDay.Done));
		Assert.Equal(2, sender.Sent.Count);

		var closed = Instance("a").CloseAddDrop("2026FA", window);
		Assert.False(Instance("b").CloseAddDrop("2026FA", window).Claimed);
		Assert.Equal(2, closed.Done);
		Assert.All(open, s => Assert.True(h.Schedule(s).IsClosed));
		Assert.False(h.Schedule(otherTerm).IsClosed);
		h.CatchUp(timetable);
		Assert.Empty(h.Timetables().OpenSchedules("2026FA"));
	}

	private sealed class FailFirstBus(ICommandPublisher inner) : ICommandPublisher {
		private int _sent;

		public void Send(ICommand command, string? exceptionMsg = null, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null) =>
			inner.Send(command, exceptionMsg, responseTimeout, ackTimeout);

		public bool TrySend(ICommand command, out CommandResponse response, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null) {
			if (Interlocked.Increment(ref _sent) == 1) {
				response = ((Command)command).Fail(new TimeoutException("injected"));
				return false;
			}
			return inner.TrySend(command, out response, responseTimeout, ackTimeout);
		}

		public bool TrySendAsync(ICommand command, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null) =>
			inner.TrySendAsync(command, responseTimeout, ackTimeout);
	}

	private sealed class RecordingSender : IReminderSender {
		public List<(Guid Student, string Term, string Kind)> Sent { get; } = [];

		public string Send(Guid studentId, string term, string kind) {
			lock (Sent)
				Sent.Add((studentId, term, kind));
			return $"student:{studentId}";
		}
	}
}
