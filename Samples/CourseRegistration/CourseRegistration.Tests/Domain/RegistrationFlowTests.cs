using CourseRegistration.Domain;
using ReactiveDomain;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using static CourseRegistration.Tests.Aggregates;

namespace CourseRegistration.Tests.Domain;

/// <summary>Registration end to end: the service, the process manager and both aggregates on RD's in-memory store.</summary>
public sealed class RegistrationFlowTests : IDisposable {
	private readonly Harness _h = new();

	public void Dispose() => _h.Dispose();

	/// <summary>
	/// The read side keeps a unique index on the catalog name. The domain refuses the duplicate
	/// first, so the index never meets a row it would reject.
	/// </summary>
	[Fact]
	public void a_section_cannot_be_offered_twice_under_any_casing() {
		var chem = _h.Offer("CHEM 101", 1);

		var refused = Assert.ThrowsAny<Exception>(() => _h.Send(new OfferSection("2026FA", "chem 101", 1, "Again", 4, 30, Times.Fri15)));

		Assert.Contains("already offered", refused.ToString());
		Assert.Equal(30, _h.Section(chem).Capacity);
	}

	[Fact]
	public void a_requested_course_is_reserved_then_added() {
		var chem = _h.Offer();
		var schedule = _h.Open();

		_h.Send(new RequestCourse(schedule, chem));
		_h.Settle();

		Assert.Equal([chem], _h.Schedule(schedule).Sections);
		Assert.Equal(1, _h.Section(chem).SeatsHeld);
	}

	[Fact]
	public void a_seat_the_schedule_turns_down_is_released() {
		var chem = _h.Offer(meetings: Times.MonWed9);
		var bio = _h.Offer("BIO 110", meetings: Times.MonWed930);
		var schedule = _h.Open();
		_h.Send(new RequestCourse(schedule, chem));
		_h.Settle();

		_h.Send(new RequestCourse(schedule, bio));
		_h.Settle();

		Assert.Equal([chem], _h.Schedule(schedule).Sections);
		Assert.Equal(0, _h.Section(bio).SeatsHeld);
	}

	[Fact]
	public void a_full_section_adds_nothing_and_holds_nothing() {
		var chem = _h.Offer(capacity: 1);
		_h.Send(new RequestCourse(_h.Open(), chem));
		_h.Settle();
		var late = _h.Open();

		_h.Send(new RequestCourse(late, chem));
		_h.Settle();

		Assert.Empty(_h.Schedule(late).Sections);
		Assert.Equal(1, _h.Section(chem).SeatsHeld);
	}

	[Fact]
	public void dropping_a_course_gives_its_seat_back() {
		var chem = _h.Offer();
		var schedule = _h.Open();
		_h.Send(new RequestCourse(schedule, chem));
		_h.Settle();

		_h.Send(new DropCourse(schedule, chem));
		_h.Settle();

		Assert.Empty(_h.Schedule(schedule).Sections);
		Assert.Equal(0, _h.Section(chem).SeatsHeld);
	}

	[Fact]
	public void a_swap_takes_the_new_seat_before_giving_up_the_old_one() {
		var early = _h.Offer(number: 1, meetings: Times.MonWed9);
		var late = _h.Offer(number: 2, meetings: Times.TueThu13);
		var schedule = _h.Open();
		_h.Send(new RequestCourse(schedule, early));
		_h.Settle();

		_h.Send(new RequestSwap(schedule, early, late));
		_h.Settle();

		Assert.Equal([late], _h.Schedule(schedule).Sections);
		Assert.Equal((0, 1), (_h.Section(early).SeatsHeld, _h.Section(late).SeatsHeld));
	}

	[Fact]
	public void a_swap_into_a_full_section_keeps_the_old_one() {
		var early = _h.Offer(number: 1);
		var full = _h.Offer(number: 2, capacity: 1, meetings: Times.TueThu13);
		_h.Send(new RequestCourse(_h.Open(), full));
		var schedule = _h.Open();
		_h.Send(new RequestCourse(schedule, early));
		_h.Settle();

		_h.Send(new RequestSwap(schedule, early, full));
		_h.Settle();

		Assert.Equal([early], _h.Schedule(schedule).Sections);
		Assert.Equal((1, 1), (_h.Section(early).SeatsHeld, _h.Section(full).SeatsHeld));
	}

	[Fact]
	public void cancelling_a_section_takes_it_off_every_schedule() {
		var chem = _h.Offer();
		var first = _h.Open();
		var second = _h.Open();
		_h.Send(new RequestCourse(first, chem));
		_h.Send(new RequestCourse(second, chem));
		_h.Settle();

		_h.Send(new CancelSection(chem));
		_h.Settle();

		Assert.Empty(_h.Schedule(first).Sections);
		Assert.Empty(_h.Schedule(second).Sections);
	}

	[Fact]
	public void the_steps_of_one_registration_share_its_correlation() {
		var chem = _h.Offer();
		var schedule = _h.Open();
		var request = MessageBuilder.New(() => new RequestCourse(schedule, chem));

		_h.Bus.Send(request);
		_h.Settle();

		var reserved = Events<SeatReserved>(_h.Connection.StreamNamer.GenerateForAggregate(typeof(SectionAgg), chem)).Single();
		var added = Events<CourseAdded>(_h.Connection.StreamNamer.GenerateForAggregate(typeof(TermScheduleAgg), schedule)).Single();
		Assert.Equal(request.CorrelationId, reserved.CorrelationId);
		Assert.Equal(request.CorrelationId, added.CorrelationId);
	}

	/// <summary>Two writers on the last seat: the stream's version check lets one in, and the other, reloaded, finds the section full.</summary>
	[Fact]
	public void two_students_racing_for_the_last_seat_cannot_both_get_it() {
		var chem = _h.Offer(capacity: 1);
		var winner = _h.Open();
		var loser = _h.Open();
		var first = _h.Repository.GetById<SectionAgg>(chem, Source());
		var second = _h.Repository.GetById<SectionAgg>(chem, Source());
		first.Reserve(winner);
		second.Reserve(loser);

		_h.Repository.Save(first);
		Assert.Throws<WrongExpectedVersionException>(() => _h.Repository.Save(second));

		_h.Send(new RequestCourse(loser, chem)); // the service retries the same way: reload, decide again
		_h.Settle();
		Assert.Equal([chem], _h.Schedule(winner).Sections);
		Assert.Empty(_h.Schedule(loser).Sections);
		Assert.Equal(1, _h.Section(chem).SeatsHeld);
	}

	/// <summary>
	/// Both seats are reserved, because each section decides alone; the schedule then takes the adds
	/// one at a time, and the second breaks the credit limit and gives its seat back.
	/// </summary>
	[Fact]
	public async Task one_student_racing_two_adds_past_the_credit_limit_keeps_one() {
		var chem = _h.Offer(credits: 4, meetings: Times.MonWed9);
		var bio = _h.Offer("BIO 110", credits: 4, meetings: Times.TueThu13);
		var schedule = _h.Open(maxCredits: 6);

		var adds = new[] { chem, bio }.Select(s => Task.Run(() => _h.Send(new RequestCourse(schedule, s)))).ToArray();
		await Task.WhenAll(adds).WaitAsync(ReactiveDomain.Testing.TestTimeouts.ThrottleWaitFor);
		_h.Settle();

		var kept = Assert.Single(_h.Schedule(schedule).Sections);
		Assert.Equal(1, _h.Section(kept).SeatsHeld);
		Assert.Equal(0, _h.Section(kept == chem ? bio : chem).SeatsHeld);
	}

	/// <summary>
	/// The process manager stops after the seat is reserved and before it sends the add. On restart it
	/// sends nothing while replaying, finds the reservation unanswered, and sends the add then.
	/// </summary>
	[Fact]
	public void a_restarted_process_finishes_the_steps_left_open() {
		var chem = _h.Offer();
		var schedule = _h.Open();
		var dropped = new DroppingBus();
		_h.StartProcess(dropped);

		_h.Send(new RequestCourse(schedule, chem));
		Harness.Eventually(() => dropped.Dropped.OfType<AddReservedCourse>().Any(), "the add was not attempted");
		Assert.Empty(_h.Schedule(schedule).Sections);
		Assert.Equal((1, 0, 0), _h.Process!.Open);

		_h.StartProcess();
		_h.Settle();

		Assert.Equal([chem], _h.Schedule(schedule).Sections);
	}

	[Fact]
	public void replaying_finished_registrations_sends_nothing() {
		var chem = _h.Offer();
		var schedule = _h.Open();
		_h.Send(new RequestCourse(schedule, chem));
		_h.Send(new DropCourse(schedule, chem));
		_h.Settle();
		var counting = new DroppingBus();

		_h.StartProcess(counting);

		Assert.Empty(counting.Dropped);
		Assert.Equal((0, 0, 0), _h.Process!.Open);
	}

	[Fact]
	public void a_command_lost_while_live_is_sent_again_without_a_restart() {
		var chem = _h.Offer();
		var schedule = _h.Open();
		var lossy = new LossyBus(_h.Bus, losses: 3);
		_h.StartProcess(lossy);

		_h.Send(new RequestCourse(schedule, chem));
		_h.Settle();

		Assert.Equal([chem], _h.Schedule(schedule).Sections);
		Assert.Equal(3, lossy.Lost);
	}

	[Fact]
	public void the_process_forgets_finished_pairs_and_keeps_enrolled_ones() {
		var chem = _h.Offer();
		var bio = _h.Offer("BIO 110", meetings: Times.TueThu13);
		var schedule = _h.Open();
		_h.Send(new RequestCourse(schedule, chem));
		_h.Send(new RequestCourse(schedule, bio));
		_h.Settle();
		_h.Send(new DropCourse(schedule, chem));
		_h.Settle();

		Assert.Equal(1, _h.Process!.Tracked);
		_h.StartProcess();
		Assert.Equal(1, _h.Process!.Tracked);
	}

	/// <summary>Sends do not wait for the handler: one that hangs holds up neither the live transition nor later events.</summary>
	[Fact]
	public void a_hung_handler_does_not_hold_up_the_live_transition_or_later_events() {
		var chem = _h.Offer();
		var bio = _h.Offer("BIO 110", meetings: Times.TueThu13);
		var first = _h.Open();
		var second = _h.Open();
		_h.StartProcess(new DroppingBus());
		_h.Send(new RequestCourse(first, chem));
		Harness.Eventually(() => _h.Process!.Open == (1, 0, 0), "the reservation was not seen");
		using var gate = new ManualResetEventSlim(false);
		using var hungBus = new Dispatcher("hung", queueCount: 1);
		var handler = new HungHandler(gate);
		using var subscription = hungBus.Subscribe<AddReservedCourse>(handler);

		var process = _h.StartProcess(hungBus);
		Harness.Eventually(() => handler.Entered, "the resend was not attempted");
		_h.Send(new RequestCourse(second, bio));

		Harness.Eventually(() => process.Open == (2, 0, 0), "an event behind the hung handler was not handled");
		gate.Set();
	}

	private List<T> Events<T>(string stream) {
		var slice = _h.Store.ReadStreamForward(stream, 0, 100);
		return slice.Events.Select(e => _h.Connection.Serializer.Deserialize(e)).OfType<T>().ToList();
	}

	/// <summary>Loses the first commands without a trace, then passes the rest through.</summary>
	private sealed class LossyBus(ICommandPublisher inner, int losses) : ICommandPublisher {
		private int _sent;

		public int Lost => Math.Min(Volatile.Read(ref _sent), losses);

		public void Send(ICommand command, string? exceptionMsg = null, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null) =>
			inner.Send(command, exceptionMsg, responseTimeout, ackTimeout);

		public bool TrySend(ICommand command, out CommandResponse response, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null) =>
			inner.TrySend(command, out response, responseTimeout, ackTimeout);

		public bool TrySendAsync(ICommand command, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null) =>
			Interlocked.Increment(ref _sent) <= losses || inner.TrySendAsync(command, responseTimeout, ackTimeout);
	}

	/// <summary>Blocks until the gate opens.</summary>
	private sealed class HungHandler(ManualResetEventSlim gate) : IHandleCommand<AddReservedCourse> {
		private volatile bool _entered;

		public bool Entered => _entered;

		public CommandResponse Handle(AddReservedCourse command) {
			_entered = true;
			gate.Wait(ReactiveDomain.Testing.TestTimeouts.ThrottleWaitFor);
			return command.Fail();
		}
	}

	/// <summary>A bus that loses every command, standing in for a process that dies before it sends.</summary>
	private sealed class DroppingBus : ICommandPublisher {
		private readonly List<ICommand> _dropped = [];

		public IReadOnlyList<ICommand> Dropped {
			get {
				lock (_dropped)
					return _dropped.ToList();
			}
		}

		public void Send(ICommand command, string? exceptionMsg = null, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null) =>
			Drop(command);

		public bool TrySend(ICommand command, out CommandResponse response, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null) {
			Drop(command);
			response = ((Command)command).Fail(new InvalidOperationException("dropped"));
			return false;
		}

		public bool TrySendAsync(ICommand command, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null) {
			Drop(command);
			return false;
		}

		private void Drop(ICommand command) {
			lock (_dropped)
				_dropped.Add(command);
		}
	}
}
