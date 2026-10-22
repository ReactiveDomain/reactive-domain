using ReactiveDomain.Messaging;

namespace CourseRegistration.Domain;

// Events and commands are defined together. None of them names a parameter CorrelationId,
// CausationId or MsgId: those are on the base records, and a parameter would hide them.

// ── Section ─────────────────────────────────────────────────────────────────

public sealed record SectionOffered(
	Guid SectionId,
	string Term,
	string CourseCode,
	int SectionNumber,
	string Title,
	int Credits,
	int Capacity,
	Meeting[] Meetings) : Event;

/// <summary>
/// The seat is held for the schedule. Carries what the schedule needs to check it, so the process
/// manager can pass it on without reading the section.
/// </summary>
public sealed record SeatReserved(
	Guid SectionId,
	Guid ScheduleId,
	string Term,
	string CourseCode,
	int Credits,
	Meeting[] Meetings,
	Guid? ReplacesSectionId) : Event;

public sealed record SeatRefused(Guid SectionId, Guid ScheduleId) : Event;

public sealed record SeatReleased(Guid SectionId, Guid ScheduleId) : Event;

public sealed record SectionRescheduled(Guid SectionId, Meeting[] Meetings) : Event;

public sealed record SectionCapacityChanged(Guid SectionId, int Capacity) : Event;

/// <summary>Every seat is released with the section; <see cref="HeldBy"/> names the schedules that held one.</summary>
public sealed record SectionCancelled(Guid SectionId, Guid[] HeldBy) : Event;

// ── Term schedule ───────────────────────────────────────────────────────────

public sealed record TermScheduleOpened(Guid ScheduleId, Guid StudentId, string Term, int MaxCredits) : Event;

public sealed record CourseAdded(
	Guid ScheduleId,
	Guid SectionId,
	string CourseCode,
	int Credits,
	Meeting[] Meetings) : Event;

public sealed record CourseSwapped(
	Guid ScheduleId,
	Guid DroppedSectionId,
	Guid AddedSectionId,
	string CourseCode,
	int Credits,
	Meeting[] Meetings) : Event;

public enum AddRejection {
	WrongTerm,
	AlreadyTakingCourse,
	CreditLimit,
	TimeConflict,
	AddDropClosed,
	NotEnrolledInDroppedSection
}

/// <summary>The schedule turned down a seat the section had already reserved; the seat must be released.</summary>
public sealed record CourseAddRejected(Guid ScheduleId, Guid SectionId, AddRejection Reason) : Event;

public sealed record CourseDropped(Guid ScheduleId, Guid SectionId) : Event;

/// <summary>The section was cancelled; its seat is already gone, so nothing is released.</summary>
public sealed record CourseCancelled(Guid ScheduleId, Guid SectionId) : Event;

public sealed record AddDropClosed(Guid ScheduleId) : Event;

// ── Commands a client sends ─────────────────────────────────────────────────

/// <summary>Offers a section; its id is <see cref="SectionAgg.IdFor"/> of the catalog name.</summary>
public sealed record OfferSection(
	string Term,
	string CourseCode,
	int SectionNumber,
	string Title,
	int Credits,
	int Capacity,
	Meeting[] Meetings) : Command;

public sealed record RescheduleSection(Guid SectionId, Meeting[] Meetings) : Command;

public sealed record ChangeSectionCapacity(Guid SectionId, int Capacity) : Command;

public sealed record CancelSection(Guid SectionId) : Command;

public sealed record OpenTermSchedule(Guid ScheduleId, Guid StudentId, string Term, int MaxCredits) : Command;

/// <summary>Asks for a seat; the course is added once the section reserves it.</summary>
public sealed record RequestCourse(Guid ScheduleId, Guid SectionId) : Command;

/// <summary>Asks for a seat in <see cref="AddSectionId"/>; <see cref="DropSectionId"/> is given up only once it is secured.</summary>
public sealed record RequestSwap(Guid ScheduleId, Guid DropSectionId, Guid AddSectionId) : Command;

public sealed record DropCourse(Guid ScheduleId, Guid SectionId) : Command;

public sealed record CloseAddDrop(Guid ScheduleId) : Command;

// ── Commands the process manager sends ──────────────────────────────────────

public sealed record AddReservedCourse(
	Guid ScheduleId,
	Guid SectionId,
	string Term,
	string CourseCode,
	int Credits,
	Meeting[] Meetings) : Command;

public sealed record SwapReservedCourse(
	Guid ScheduleId,
	Guid DropSectionId,
	Guid AddSectionId,
	string Term,
	string CourseCode,
	int Credits,
	Meeting[] Meetings) : Command;

public sealed record ReleaseSeat(Guid SectionId, Guid ScheduleId) : Command;

public sealed record RemoveCancelledCourse(Guid ScheduleId, Guid SectionId) : Command;
