using ReactiveDomain;
using ReactiveDomain.Messaging;
using ReactiveDomain.Util;

namespace CourseRegistration.Domain;

/// <summary>
/// One student's courses for one term. Sits between the student and the sections, and owns every
/// rule about that student: the credit limit, no time conflicts, no course twice, and the add/drop
/// window.
/// </summary>
/// <remarks>
/// It never holds a seat. A course is added only after its section has reserved one, so the check
/// here and the add are one atomic step. A rejection is raised as an event rather than thrown,
/// because the seat already reserved for it has to be released.
/// </remarks>
public sealed class TermScheduleAgg : AggregateRoot {
	private readonly Dictionary<Guid, Entry> _entries = [];
	private readonly HashSet<Guid> _answered = [];
	private int _maxCredits;

	/// <summary>The student the schedule belongs to.</summary>
	public Guid StudentId { get; private set; }

	/// <summary>The term the schedule covers.</summary>
	public string Term { get; private set; } = string.Empty;

	/// <summary>True once add/drop has closed for the schedule.</summary>
	public bool IsClosed { get; private set; }

	/// <summary>The sections the student is enrolled in.</summary>
	public IReadOnlyCollection<Guid> Sections => _entries.Keys;

	/// <summary>The credits the student is enrolled for.</summary>
	public int Credits => _entries.Values.Sum(e => e.Credits);

	private TermScheduleAgg() {
		RegisterEvents();
	}

	/// <summary>Opens a student's schedule for a term.</summary>
	public TermScheduleAgg(Guid id, Guid studentId, string term, int maxCredits, ICorrelatedMessage source) : this() {
		Ensure.NotEmptyGuid(id, nameof(id));
		Ensure.NotEmptyGuid(studentId, nameof(studentId));
		term = Text.Required(term, nameof(term));
		Ensure.Positive(maxCredits, nameof(maxCredits));
		Source = source;
		Raise(new TermScheduleOpened(id, studentId, term, maxCredits));
	}

	private void RegisterEvents() {
		Register<TermScheduleOpened>(Apply);
		Register<CourseAdded>(Apply);
		Register<CourseSwapped>(Apply);
		Register<CourseDropped>(e => _entries.Remove(e.SectionId));
		Register<CourseCancelled>(e => _entries.Remove(e.SectionId));
		Register<AddDropClosed>(_ => IsClosed = true);
		Register<CourseAddRejected>(e => _answered.Add(e.ReservationId));
	}

	/// <summary>Adds a section whose seat is already reserved, or records why the student cannot take it.</summary>
	/// <remarks>Idempotent: a reservation already answered, or a section already on the schedule, raises nothing.</remarks>
	public void Add(Guid reservationId, Guid sectionId, string term, string courseCode, int credits, Meeting[] meetings) {
		Ensure.NotEmptyGuid(reservationId, nameof(reservationId));
		Ensure.NotEmptyGuid(sectionId, nameof(sectionId));
		// Each reservation is answered once, whatever the answer was: a resent command must not raise
		// a second rejection (the process manager counts answers) or re-add a course dropped since.
		if (_answered.Contains(reservationId) || _entries.ContainsKey(sectionId))
			return;
		var entry = new Entry(courseCode, credits, meetings);
		if (Check(term, entry, replacing: null) is { } reason) {
			Raise(new CourseAddRejected(Id, sectionId, reservationId, reason));
			return;
		}
		Raise(new CourseAdded(Id, sectionId, reservationId, courseCode, credits, meetings));
	}

	/// <summary>Replaces <paramref name="dropSectionId"/> with a section whose seat is already reserved, in one change.</summary>
	/// <remarks>
	/// The student always holds one of the two: if the new section is turned down, the old one stays.
	/// Idempotent: a reservation already answered raises nothing.
	/// </remarks>
	public void Swap(Guid reservationId, Guid dropSectionId, Guid addSectionId, string term, string courseCode, int credits, Meeting[] meetings) {
		Ensure.NotEmptyGuid(reservationId, nameof(reservationId));
		Ensure.NotEmptyGuid(dropSectionId, nameof(dropSectionId));
		Ensure.NotEmptyGuid(addSectionId, nameof(addSectionId));
		// The answered check must come first: after the swap and later changes, the entries alone
		// cannot tell a resend from a fresh request, and judging a resend against the current entries
		// would reject — and so release the seat of — a course the student holds.
		if (_answered.Contains(reservationId))
			return;
		if (_entries.ContainsKey(addSectionId) && !_entries.ContainsKey(dropSectionId))
			return;
		var entry = new Entry(courseCode, credits, meetings);
		var reason = _entries.ContainsKey(dropSectionId)
			? Check(term, entry, replacing: dropSectionId)
			: IsClosed ? AddRejection.AddDropClosed : AddRejection.NotEnrolledInDroppedSection;
		if (reason is not null) {
			Raise(new CourseAddRejected(Id, addSectionId, reservationId, reason.Value));
			return;
		}
		Raise(new CourseSwapped(Id, dropSectionId, addSectionId, reservationId, courseCode, credits, meetings));
	}

	/// <summary>Drops a section the student is enrolled in.</summary>
	/// <remarks>Idempotent: a section not on the schedule raises nothing.</remarks>
	/// <exception cref="InvalidOperationException">Add/drop has closed.</exception>
	public void Drop(Guid sectionId) {
		if (!_entries.ContainsKey(sectionId))
			return;
		if (IsClosed)
			throw new InvalidOperationException("Add/drop has closed for this schedule.");
		Raise(new CourseDropped(Id, sectionId));
	}

	/// <summary>Takes a cancelled section off the schedule, whether or not add/drop is open.</summary>
	public void RemoveCancelled(Guid sectionId) {
		if (!_entries.ContainsKey(sectionId))
			return;
		Raise(new CourseCancelled(Id, sectionId));
	}

	/// <summary>Closes add/drop: no more adds, swaps or drops.</summary>
	public void Close() {
		if (IsClosed)
			return;
		Raise(new AddDropClosed(Id));
	}

	private AddRejection? Check(string term, Entry candidate, Guid? replacing) {
		if (IsClosed)
			return AddRejection.AddDropClosed;
		if (!string.Equals(term, Term, StringComparison.OrdinalIgnoreCase))
			return AddRejection.WrongTerm;
		var kept = _entries.Where(e => e.Key != replacing).Select(e => e.Value).ToList();
		if (kept.Any(e => string.Equals(e.CourseCode, candidate.CourseCode, StringComparison.OrdinalIgnoreCase)))
			return AddRejection.AlreadyTakingCourse;
		if (kept.Sum(e => e.Credits) + candidate.Credits > _maxCredits)
			return AddRejection.CreditLimit;
		if (kept.SelectMany(e => e.Meetings).ConflictsWith(candidate.Meetings))
			return AddRejection.TimeConflict;
		return null;
	}

	private void Apply(TermScheduleOpened e) {
		Id = e.ScheduleId;
		StudentId = e.StudentId;
		Term = e.Term;
		_maxCredits = e.MaxCredits;
	}

	private void Apply(CourseAdded e) {
		_entries[e.SectionId] = new Entry(e.CourseCode, e.Credits, e.Meetings);
		_answered.Add(e.ReservationId);
	}

	private void Apply(CourseSwapped e) {
		_entries.Remove(e.DroppedSectionId);
		_entries[e.AddedSectionId] = new Entry(e.CourseCode, e.Credits, e.Meetings);
		_answered.Add(e.ReservationId);
	}

	private sealed record Entry(string CourseCode, int Credits, Meeting[] Meetings);
}
