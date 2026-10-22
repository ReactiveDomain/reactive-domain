using ReactiveDomain;
using ReactiveDomain.Messaging;
using ReactiveDomain.Util;

namespace CourseRegistration.Domain;

/// <summary>
/// One offering of a course in one term. Owns the only rule that spans students: no more seats are
/// held than the section has.
/// </summary>
/// <remarks>
/// A seat is held by a schedule id. Whether that student is enrolled is the schedule's business, so
/// there is no held/taken distinction here: a reservation is the seat.
/// </remarks>
public sealed class SectionAgg : AggregateRoot {
	private readonly HashSet<Guid> _holders = [];
	private string _term = string.Empty;
	private string _courseCode = string.Empty;
	private int _credits;
	private Meeting[] _meetings = [];
	private bool _cancelled;

	/// <summary>How many seats the section offers.</summary>
	public int Capacity { get; private set; }

	/// <summary>How many seats are held.</summary>
	public int SeatsHeld => _holders.Count;

	private SectionAgg() {
		RegisterEvents();
	}

	/// <summary>Offers a new section.</summary>
	public SectionAgg(
		Guid id,
		string term,
		string courseCode,
		int sectionNumber,
		string title,
		int credits,
		int capacity,
		Meeting[] meetings,
		ICorrelatedMessage source) : this() {
		Ensure.NotEmptyGuid(id, nameof(id));
		term = Text.Required(term, nameof(term));
		courseCode = Text.Required(courseCode, nameof(courseCode));
		title = Text.Required(title, nameof(title));
		Ensure.Positive(sectionNumber, nameof(sectionNumber));
		Ensure.Positive(credits, nameof(credits));
		Ensure.Nonnegative(capacity, nameof(capacity));
		Ensure.NotNull(meetings, nameof(meetings));
		Source = source;
		Raise(new SectionOffered(id, term, courseCode, sectionNumber, title, credits, capacity, meetings));
	}

	private void RegisterEvents() {
		Register<SectionOffered>(Apply);
		Register<SeatReserved>(Apply);
		Register<SeatReleased>(Apply);
		Register<SectionRescheduled>(Apply);
		Register<SectionCapacityChanged>(Apply);
		Register<SectionCancelled>(Apply);
		RegisterPassThrough<SeatRefused>();
	}

	/// <summary>Holds a seat for <paramref name="scheduleId"/>, or records that the section is full.</summary>
	/// <param name="scheduleId">The schedule the seat is for.</param>
	/// <param name="replacesSectionId">For a swap, the section the schedule gives up once this seat is secured.</param>
	/// <remarks>Idempotent: a schedule that already holds a seat raises nothing.</remarks>
	/// <exception cref="InvalidOperationException">The section is cancelled.</exception>
	public void Reserve(Guid scheduleId, Guid? replacesSectionId = null) {
		Ensure.NotEmptyGuid(scheduleId, nameof(scheduleId));
		if (_cancelled)
			throw new InvalidOperationException("A cancelled section has no seats to reserve.");
		if (_holders.Contains(scheduleId))
			return;
		if (_holders.Count >= Capacity) {
			Raise(new SeatRefused(Id, scheduleId));
			return;
		}
		Raise(new SeatReserved(Id, scheduleId, _term, _courseCode, _credits, _meetings, replacesSectionId));
	}

	/// <summary>Gives up the seat <paramref name="scheduleId"/> holds.</summary>
	/// <remarks>Idempotent: a schedule holding no seat raises nothing, so a repeated release is harmless.</remarks>
	public void Release(Guid scheduleId) {
		Ensure.NotEmptyGuid(scheduleId, nameof(scheduleId));
		if (!_holders.Contains(scheduleId))
			return;
		Raise(new SeatReleased(Id, scheduleId));
	}

	/// <summary>Moves the section's meetings.</summary>
	/// <remarks>
	/// Students already enrolled keep their seats even if the new times conflict with another course;
	/// that is a registrar's call, not something to unwind automatically.
	/// </remarks>
	public void Reschedule(Meeting[] meetings) {
		Ensure.NotNull(meetings, nameof(meetings));
		EnsureNotCancelled();
		if (_meetings.SameAs(meetings))
			return;
		Raise(new SectionRescheduled(Id, meetings));
	}

	/// <summary>Changes how many seats the section offers.</summary>
	/// <remarks>Lowering it below the seats held keeps those seats and refuses new reservations.</remarks>
	public void ChangeCapacity(int capacity) {
		Ensure.Nonnegative(capacity, nameof(capacity));
		EnsureNotCancelled();
		if (capacity == Capacity)
			return;
		Raise(new SectionCapacityChanged(Id, capacity));
	}

	/// <summary>Cancels the section, releasing every seat.</summary>
	public void Cancel() {
		if (_cancelled)
			return;
		Raise(new SectionCancelled(Id, _holders.ToArray()));
	}

	private void EnsureNotCancelled() {
		if (_cancelled)
			throw new InvalidOperationException("The section is cancelled.");
	}

	private void Apply(SectionOffered e) {
		Id = e.SectionId;
		_term = e.Term;
		_courseCode = e.CourseCode;
		_credits = e.Credits;
		_meetings = e.Meetings;
		Capacity = e.Capacity;
	}

	private void Apply(SeatReserved e) => _holders.Add(e.ScheduleId);

	private void Apply(SeatReleased e) => _holders.Remove(e.ScheduleId);

	private void Apply(SectionRescheduled e) => _meetings = e.Meetings;

	private void Apply(SectionCapacityChanged e) => Capacity = e.Capacity;

	private void Apply(SectionCancelled e) {
		_cancelled = true;
		_holders.Clear();
	}
}
