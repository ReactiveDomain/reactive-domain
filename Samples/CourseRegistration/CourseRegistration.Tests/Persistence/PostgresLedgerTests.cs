using CourseRegistration.Domain;
using CourseRegistration.ReadModels;
using CourseRegistration.ReadModels.Persistence;
using ReactiveDomain.Messaging;

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

		var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => Task.Run(() => (i, claims.TryClaim("job", window, $"instance-{i}")))));

		var (winner, _) = Assert.Single(results, r => r.Item2);
		Assert.Equal(new JobRun($"instance-{winner}", false), claims.Run("job", window));
		Assert.True(claims.TryClaim("job", window.AddDays(1), "instance-0"));
		Assert.False(claims.Complete("job", window, $"instance-{(winner + 1) % 8}"));
		Assert.True(claims.Complete("job", window, $"instance-{winner}"));
		Assert.True(claims.Run("job", window)!.Completed);
		Assert.Null(claims.Run("job", window.AddDays(2)));
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
			new(new JobClaims(data), new ReminderLedger(data), h.Timetables(), h.Bus, sender, name);
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

	private sealed class RecordingSender : IReminderSender {
		public List<(Guid Student, string Term, string Kind)> Sent { get; } = [];

		public string Send(Guid studentId, string term, string kind) {
			lock (Sent)
				Sent.Add((studentId, term, kind));
			return $"student:{studentId}";
		}
	}
}
