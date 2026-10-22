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

	private List<T> Events<T>(string stream) {
		var slice = _h.Store.ReadStreamForward(stream, 0, 100);
		return slice.Events.Select(e => _h.Connection.Serializer.Deserialize(e)).OfType<T>().ToList();
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
