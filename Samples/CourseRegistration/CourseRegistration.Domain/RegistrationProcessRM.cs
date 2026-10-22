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
/// it sends nothing, because the commands it would send were sent the first time. What it learns
/// instead is which steps are still open, and once live it sends exactly those again. Every command it
/// sends is idempotent, so a step that had in fact completed changes nothing.</para>
/// <para>The two categories are read side by side, so catching up, a schedule's answer can arrive
/// before the reservation it answers. Open steps are therefore counted, not sequenced: each seat
/// reserved is answered by exactly one add, swap or rejection, and each seat given up by exactly one
/// release, so a step is open while one side has counted more than the other, whichever arrived
/// first. A pair whose counts balance, and that is not enrolled, is forgotten.</para>
/// <para>Commands are sent with <see cref="ICommandPublisher.TrySendAsync"/>, and no send result is awaited.
/// Give it a <see cref="Dispatcher"/> with at least one publish queue: with none, RD runs the handler
/// on the sending thread, which here is the queue thread, so a slow handler would hold up the events
/// behind it. A step is done when the event that closes it
/// arrives, whatever happened to the command: one that failed, timed out or was lost leaves its step
/// open, and every step open longer than the retry delay is sent again until it closes.</para>
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
	IHandle<CourseCancelled>,
	IHandle<RegistrationProcessRM.RetryOpenSteps> {
	private static readonly ILogger Log = LogManager.GetLogger(typeof(RegistrationProcessRM).FullName!);

	private readonly ICommandPublisher _bus;
	private readonly TimeSpan _retryDelay;
	private Timer? _retryTimer;
	private bool _live;
	private volatile bool _anyOpen;

	private readonly Dictionary<(Guid SectionId, Guid ScheduleId), Slot> _slots = [];
	private readonly HashSet<Guid> _cancelled = [];

	/// <summary>Asks the process manager to resend the steps open longer than the retry delay. Its timer sends this while any step is open.</summary>
	public sealed record RetryOpenSteps : Message;

	/// <summary>Creates the process manager; nothing is read or sent until <see cref="StartAsync"/>.</summary>
	/// <param name="connection">Where the section and schedule categories are read from.</param>
	/// <param name="bus">Where commands are sent once live; a <see cref="Dispatcher"/> with at least one publish queue.</param>
	/// <param name="retryDelay">How long a step may stay open before it is sent again; 5 seconds when null.</param>
	public RegistrationProcessRM(IConfiguredConnection connection, ICommandPublisher bus, TimeSpan? retryDelay = null)
		: base(nameof(RegistrationProcessRM), connection) {
		_bus = bus;
		_retryDelay = retryDelay ?? TimeSpan.FromSeconds(5);
		EventStream.Subscribe<SeatReserved>(this);
		EventStream.Subscribe<SeatReleased>(this);
		EventStream.Subscribe<SectionCancelled>(this);
		EventStream.Subscribe<CourseAdded>(this);
		EventStream.Subscribe<CourseSwapped>(this);
		EventStream.Subscribe<CourseAddRejected>(this);
		EventStream.Subscribe<CourseDropped>(this);
		EventStream.Subscribe<CourseCancelled>(this);
		EventStream.Subscribe<RetryOpenSteps>(this);
	}

	/// <summary>Reads both categories, then sends every step left open. Completes once live.</summary>
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

	/// <summary>How many section/schedule pairs are remembered: those with a step open, or enrolled.</summary>
	public int Tracked {
		get {
			lock (ReaderLock) {
				return _slots.Count;
			}
		}
	}

	private void GoLive() {
		_live = true;
		Resend(olderThan: TimeSpan.Zero);
		_retryTimer = new Timer(_ => {
			if (!_anyOpen)
				return;
			try {
				Publish(new RetryOpenSteps());
			} catch (ObjectDisposedException) {
				// A tick that raced Dispose.
			}
		}, null, _retryDelay, _retryDelay);
	}

	void IHandle<RetryOpenSteps>.Handle(RetryOpenSteps message) => Resend(olderThan: _retryDelay);

	private void Resend(TimeSpan olderThan) {
		var due = DateTime.UtcNow - olderThan;
		var anyOpen = false;
		foreach (var slot in _slots.Values.ToList()) {
			var open = AwaitingSchedule(slot) || AwaitingRelease(slot) || AwaitingRemoval(slot);
			anyOpen |= open;
			if (!open || slot.SentAt > due)
				continue;
			// The original cause is not kept, so a resend starts its own trace.
			var source = new CorrelationSource(Guid.NewGuid(), Guid.Empty, Guid.NewGuid());
			if (AwaitingSchedule(slot))
				SendAdd(slot);
			if (AwaitingRelease(slot))
				Send(slot, source, new ReleaseSeat(slot.SectionId, slot.ScheduleId));
			if (AwaitingRemoval(slot))
				Send(slot, source, new RemoveCancelledCourse(slot.ScheduleId, slot.SectionId));
		}
		_anyOpen = anyOpen;
	}

	void IHandle<SeatReserved>.Handle(SeatReserved e) {
		var slot = At(e.SectionId, e.ScheduleId);
		slot.Reserved++;
		slot.LastReservation = e;
		if (AwaitingSchedule(slot))
			SendAdd(slot);
		Forget(slot);
	}

	void IHandle<SeatReleased>.Handle(SeatReleased e) {
		var slot = At(e.SectionId, e.ScheduleId);
		slot.Released++;
		Forget(slot);
	}

	void IHandle<SectionCancelled>.Handle(SectionCancelled e) {
		_cancelled.Add(e.SectionId);
		foreach (var slot in _slots.Values.Where(s => s.SectionId == e.SectionId).ToList()) {
			if (AwaitingRemoval(slot))
				Send(slot, e, new RemoveCancelledCourse(slot.ScheduleId, slot.SectionId));
			Forget(slot);
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

	void IHandle<CourseCancelled>.Handle(CourseCancelled e) {
		var slot = At(e.SectionId, e.ScheduleId);
		slot.Enrolled = false;
		Forget(slot);
	}

	private void Enrolled(Slot slot, ICorrelatedMessage cause) {
		slot.Answered++;
		slot.Enrolled = true;
		// The add raced the cancel and won; take it back off.
		if (AwaitingRemoval(slot))
			Send(slot, cause, new RemoveCancelledCourse(slot.ScheduleId, slot.SectionId));
	}

	private void GaveUp(Slot slot, ICorrelatedMessage cause, bool enrolled) {
		slot.Enrolled = enrolled;
		slot.ReleasesAsked++;
		if (AwaitingRelease(slot))
			Send(slot, cause, new ReleaseSeat(slot.SectionId, slot.ScheduleId));
		Forget(slot);
	}

	// A cancelled section answers nothing and releases nothing more: the cancel released every seat.
	private bool AwaitingSchedule(Slot slot) => slot.Reserved > slot.Answered && !_cancelled.Contains(slot.SectionId);

	private bool AwaitingRelease(Slot slot) => slot.ReleasesAsked > slot.Released && !_cancelled.Contains(slot.SectionId);

	private bool AwaitingRemoval(Slot slot) => slot.Enrolled && _cancelled.Contains(slot.SectionId);

	// Only differences between the counts matter, so a pair that balances can start again from zero.
	private void Forget(Slot slot) {
		if (slot.Enrolled)
			return;
		var balanced = slot.Reserved == slot.Answered && slot.ReleasesAsked == slot.Released;
		if (balanced || _cancelled.Contains(slot.SectionId))
			_slots.Remove((slot.SectionId, slot.ScheduleId));
	}

	private void SendAdd(Slot slot) {
		var e = slot.LastReservation!;
		Command command = e.ReplacesSectionId is { } drop
			? new SwapReservedCourse(e.ScheduleId, drop, e.SectionId, e.Term, e.CourseCode, e.Credits, e.Meetings)
			: new AddReservedCourse(e.ScheduleId, e.SectionId, e.Term, e.CourseCode, e.Credits, e.Meetings);
		Send(slot, e, command);
	}

	// Catching up, nothing is sent: the steps still open are sent once, at the live transition.
	private void Send(Slot slot, ICorrelatedMessage cause, Command command) {
		if (!_live)
			return;
		command.CorrelationId = cause.CorrelationId;
		command.CausationId = cause.MsgId;
		slot.SentAt = DateTime.UtcNow;
		_anyOpen = true;
		if (!_bus.TrySendAsync(command))
			Log.Error("{0} was not dispatched; it is sent again while its step stays open", command.GetType().Name);
	}

	/// <inheritdoc/>
	protected override void Dispose(bool disposing) {
		if (disposing)
			_retryTimer?.Dispose();
		base.Dispose(disposing);
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
		public DateTime SentAt { get; set; }
	}
}
