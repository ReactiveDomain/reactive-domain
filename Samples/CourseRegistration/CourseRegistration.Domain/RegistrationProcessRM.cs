using ReactiveDomain;
using ReactiveDomain.Foundation;
using ReactiveDomain.Logging;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;

namespace CourseRegistration.Domain;

/// <summary>
/// Carries a registration across the section and the schedule: a reserved seat becomes an add (or a
/// swap), a rejected, dropped or swapped-out course gives its seat back, and a cancelled section is
/// taken off every schedule that has it.
/// </summary>
/// <remarks>
/// <para>State is rebuilt from both categories on every start; nothing is persisted. While catching up
/// it sends nothing (<see cref="NullBus"/>), because the commands it would send were sent the first
/// time. What it learns instead is which steps are still open, and once live it sends exactly those
/// again. Every command it sends is idempotent, so a step that had in fact completed changes
/// nothing.</para>
/// <para>The two categories are read side by side, so catching up, a schedule's answer can arrive
/// before the reservation it answers. Open steps are therefore counted, not sequenced: each seat
/// reserved is answered by exactly one add, swap or rejection, and each seat given up by exactly one
/// release, so a step is open while one side has counted more than the other, whichever arrived
/// first.</para>
/// </remarks>
public sealed class RegistrationProcessRM :
	ReadModelBase,
	IHandle<SeatReserved>,
	IHandle<SeatReleased>,
	IHandle<SectionCancelled>,
	IHandle<CourseAdded>,
	IHandle<CourseSwapped>,
	IHandle<CourseAddRejected>,
	IHandle<CourseDropped>,
	IHandle<CourseCancelled> {
	private static readonly ILogger Log = LogManager.GetLogger(typeof(RegistrationProcessRM).FullName!);
	private static readonly NullBus ReplayBus = new();

	private readonly ICommandPublisher _liveBus;
	private ICommandPublisher _bus = ReplayBus;

	private readonly Dictionary<(Guid SectionId, Guid ScheduleId), Slot> _slots = [];
	private readonly HashSet<Guid> _cancelled = [];

	/// <summary>Creates the process manager; nothing is read or sent until <see cref="StartAsync"/>.</summary>
	public RegistrationProcessRM(IConfiguredConnection connection, ICommandPublisher bus)
		: base(nameof(RegistrationProcessRM), connection) {
		_liveBus = bus;
		EventStream.Subscribe<SeatReserved>(this);
		EventStream.Subscribe<SeatReleased>(this);
		EventStream.Subscribe<SectionCancelled>(this);
		EventStream.Subscribe<CourseAdded>(this);
		EventStream.Subscribe<CourseSwapped>(this);
		EventStream.Subscribe<CourseAddRejected>(this);
		EventStream.Subscribe<CourseDropped>(this);
		EventStream.Subscribe<CourseCancelled>(this);
	}

	/// <summary>Reads both categories, then resends every step left open. Completes once that is done.</summary>
	public Task StartAsync() =>
		StartAllAsync(
			[StreamStarter.ForCategory<SectionAgg>(), StreamStarter.ForCategory<TermScheduleAgg>()],
			onceLive: GoLive);

	/// <summary>Steps not yet seen through: reservations awaiting the schedule, seats awaiting release, and courses awaiting removal.</summary>
	public (int AwaitingSchedule, int AwaitingRelease, int AwaitingRemoval) Open {
		get {
			lock (ReaderLock) {
				return (_slots.Values.Count(AwaitingSchedule), _slots.Values.Count(AwaitingRelease), _slots.Values.Count(AwaitingRemoval));
			}
		}
	}

	private void GoLive() {
		_bus = _liveBus;
		foreach (var slot in _slots.Values.ToList()) {
			// Resent after a restart: the original cause is not kept, so each resend starts its own trace.
			var source = new CorrelationSource(Guid.NewGuid(), Guid.Empty, Guid.NewGuid());
			if (AwaitingSchedule(slot))
				SendAdd(slot);
			if (AwaitingRelease(slot))
				Send(source, new ReleaseSeat(slot.SectionId, slot.ScheduleId));
			if (AwaitingRemoval(slot))
				Send(source, new RemoveCancelledCourse(slot.ScheduleId, slot.SectionId));
		}
	}

	void IHandle<SeatReserved>.Handle(SeatReserved e) {
		var slot = At(e.SectionId, e.ScheduleId);
		slot.Reserved++;
		slot.LastReservation = e;
		if (AwaitingSchedule(slot))
			SendAdd(slot);
	}

	void IHandle<SeatReleased>.Handle(SeatReleased e) => At(e.SectionId, e.ScheduleId).Released++;

	void IHandle<SectionCancelled>.Handle(SectionCancelled e) {
		_cancelled.Add(e.SectionId);
		foreach (var slot in _slots.Values.Where(s => s.SectionId == e.SectionId).ToList()) {
			if (AwaitingRemoval(slot))
				Send(e, new RemoveCancelledCourse(slot.ScheduleId, slot.SectionId));
		}
	}

	void IHandle<CourseAdded>.Handle(CourseAdded e) => Enrolled(At(e.SectionId, e.ScheduleId), e);

	void IHandle<CourseSwapped>.Handle(CourseSwapped e) {
		GaveUp(At(e.DroppedSectionId, e.ScheduleId), e, enrolled: false);
		Enrolled(At(e.AddedSectionId, e.ScheduleId), e);
	}

	void IHandle<CourseAddRejected>.Handle(CourseAddRejected e) {
		var slot = At(e.SectionId, e.ScheduleId);
		slot.Answered++;
		GaveUp(slot, e, enrolled: slot.Enrolled);
	}

	void IHandle<CourseDropped>.Handle(CourseDropped e) => GaveUp(At(e.SectionId, e.ScheduleId), e, enrolled: false);

	void IHandle<CourseCancelled>.Handle(CourseCancelled e) => At(e.SectionId, e.ScheduleId).Enrolled = false;

	private void Enrolled(Slot slot, ICorrelatedMessage cause) {
		slot.Answered++;
		slot.Enrolled = true;
		// The add raced the cancel and won; take it back off.
		if (AwaitingRemoval(slot))
			Send(cause, new RemoveCancelledCourse(slot.ScheduleId, slot.SectionId));
	}

	private void GaveUp(Slot slot, ICorrelatedMessage cause, bool enrolled) {
		slot.Enrolled = enrolled;
		slot.ReleasesAsked++;
		if (AwaitingRelease(slot))
			Send(cause, new ReleaseSeat(slot.SectionId, slot.ScheduleId));
	}

	// A cancelled section answers nothing and releases nothing more: the cancel released every seat.
	private bool AwaitingSchedule(Slot slot) => slot.Reserved > slot.Answered && !_cancelled.Contains(slot.SectionId);

	private bool AwaitingRelease(Slot slot) => slot.ReleasesAsked > slot.Released && !_cancelled.Contains(slot.SectionId);

	private bool AwaitingRemoval(Slot slot) => slot.Enrolled && _cancelled.Contains(slot.SectionId);

	private void SendAdd(Slot slot) {
		var e = slot.LastReservation!;
		Command command = e.ReplacesSectionId is { } drop
			? new SwapReservedCourse(e.ScheduleId, drop, e.SectionId, e.Term, e.CourseCode, e.Credits, e.Meetings)
			: new AddReservedCourse(e.ScheduleId, e.SectionId, e.Term, e.CourseCode, e.Credits, e.Meetings);
		Send(e, command);
	}

	// A failed send stays open and is sent again on the next start.
	private void Send(ICorrelatedMessage cause, Command command) {
		command.CorrelationId = cause.CorrelationId;
		command.CausationId = cause.MsgId;
		if (!_bus.TrySend(command, out var response))
			Log.Error("{0} failed and stays open until the next start: {1}", command.GetType().Name,
				(response as Fail)?.Exception?.Message ?? "no response");
	}

	private Slot At(Guid sectionId, Guid scheduleId) {
		if (!_slots.TryGetValue((sectionId, scheduleId), out var slot))
			_slots[(sectionId, scheduleId)] = slot = new Slot(sectionId, scheduleId);
		return slot;
	}

	/// <summary>One schedule's dealings with one section, as counted from both sides.</summary>
	private sealed class Slot(Guid sectionId, Guid scheduleId) {
		public Guid SectionId { get; } = sectionId;
		public Guid ScheduleId { get; } = scheduleId;
		public int Reserved { get; set; }
		public int Answered { get; set; }
		public int ReleasesAsked { get; set; }
		public int Released { get; set; }
		public bool Enrolled { get; set; }
		public SeatReserved? LastReservation { get; set; }
	}
}
