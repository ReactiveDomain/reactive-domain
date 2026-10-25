using CourseRegistration.Domain;
using CourseRegistration.ReadModels.Persistence;
using ReactiveDomain.Foundation;
using ReactiveDomain.Messaging.Bus;

namespace CourseRegistration.ReadModels;

/// <summary>
/// Each student's timetable, built from two categories: schedules say which sections a student has,
/// sections say what those sections are. Saves one checkpoint per category.
/// </summary>
/// <remarks>
/// <para>The categories are read side by side, so nothing orders a schedule event against a section
/// event: catching up, a <see cref="CourseAdded"/> can arrive before its section's
/// <see cref="SectionOffered"/>. Each row is therefore built from whichever half is known, and the
/// section's own stream, which is ordered, has the last word on what the section is.</para>
/// <para>A rescheduled section rewrites every timetable entry for it in one flush.</para>
/// </remarks>
[OwnedTables(typeof(TimetableSectionRow), typeof(ScheduleRow), typeof(TimetableEntryRow))]
public sealed class StudentTimetableRM :
	ProjectionRM,
	IHandle<SectionOffered>,
	IHandle<SectionRescheduled>,
	IHandle<TermScheduleOpened>,
	IHandle<CourseAdded>,
	IHandle<CourseSwapped>,
	IHandle<CourseDropped>,
	IHandle<CourseCancelled>,
	IHandle<AddDropClosed> {
	private readonly IRowStore<Guid, TimetableSectionRow> _sectionStore;
	private readonly IRowStore<Guid, ScheduleRow> _scheduleStore;
	private readonly IRowStore<Guid, TimetableEntryRow> _entryStore;
	private readonly WriteBuffer<Guid, TimetableSectionRow> _sectionWrites;
	private readonly WriteBuffer<Guid, ScheduleRow> _scheduleWrites;
	private readonly WriteBuffer<Guid, TimetableEntryRow> _entryWrites;

	private readonly Dictionary<Guid, TimetableSectionRow> _sections = [];
	private readonly Dictionary<Guid, ScheduleRow> _schedules = [];
	private readonly Dictionary<Guid, TimetableEntryRow> _entries = [];
	private readonly Dictionary<Guid, HashSet<Guid>> _entriesBySection = [];
	private readonly Dictionary<Guid, HashSet<Guid>> _entriesBySchedule = [];

	/// <summary>Creates the projection; call <see cref="ProjectionRM.StartAsync"/> to run it.</summary>
	public StudentTimetableRM(IConfiguredConnection connection, IProjectionDb db)
		: base(nameof(StudentTimetableRM), connection, db) {
		_sectionStore = db.Rows<Guid, TimetableSectionRow>();
		_scheduleStore = db.Rows<Guid, ScheduleRow>();
		_entryStore = db.Rows<Guid, TimetableEntryRow>();
		_sectionWrites = OwnTable(_sectionStore);
		_scheduleWrites = OwnTable(_scheduleStore);
		_entryWrites = OwnTable(_entryStore);
		EventStream.Subscribe<SectionOffered>(this);
		EventStream.Subscribe<SectionRescheduled>(this);
		EventStream.Subscribe<TermScheduleOpened>(this);
		EventStream.Subscribe<CourseAdded>(this);
		EventStream.Subscribe<CourseSwapped>(this);
		EventStream.Subscribe<CourseDropped>(this);
		EventStream.Subscribe<CourseCancelled>(this);
		EventStream.Subscribe<AddDropClosed>(this);
	}

	/// <inheritdoc/>
	protected override void Load() {
		foreach (var row in _sectionStore.All())
			_sections[row.SectionId] = row;
		foreach (var row in _scheduleStore.All())
			_schedules[row.ScheduleId] = row;
		foreach (var row in _entryStore.All())
			Index(row);
	}

	/// <inheritdoc/>
	protected override IReadOnlyList<StreamStarter> Streams() => [Category<SectionAgg>(), Category<TermScheduleAgg>()];

	void IHandle<SectionOffered>.Handle(SectionOffered e) =>
		Section(e.SectionId, s => {
			s.CourseCode = e.CourseCode;
			s.SectionNumber = e.SectionNumber;
			s.Title = e.Title;
			s.MeetingsJson = MeetingJson.Write(e.Meetings);
		});

	void IHandle<SectionRescheduled>.Handle(SectionRescheduled e) =>
		Section(e.SectionId, s => s.MeetingsJson = MeetingJson.Write(e.Meetings));

	void IHandle<TermScheduleOpened>.Handle(TermScheduleOpened e) {
		var row = Schedule(e.ScheduleId);
		row.StudentId = e.StudentId;
		row.Term = e.Term;
		_scheduleWrites.Upsert(row.ScheduleId, row);
		// Entries recorded before the schedule's own first event was read get their student now.
		foreach (var id in EntriesOf(_entriesBySchedule, e.ScheduleId))
			Write(_entries[id], x => {
				x.StudentId = e.StudentId;
				x.Term = e.Term;
			});
	}

	void IHandle<CourseAdded>.Handle(CourseAdded e) =>
		Add(e.ScheduleId, e.SectionId, e.CourseCode, e.Credits, e.Meetings);

	void IHandle<CourseSwapped>.Handle(CourseSwapped e) {
		Remove(e.ScheduleId, e.DroppedSectionId);
		Add(e.ScheduleId, e.AddedSectionId, e.CourseCode, e.Credits, e.Meetings);
	}

	void IHandle<CourseDropped>.Handle(CourseDropped e) => Remove(e.ScheduleId, e.SectionId);

	void IHandle<CourseCancelled>.Handle(CourseCancelled e) => Remove(e.ScheduleId, e.SectionId);

	void IHandle<AddDropClosed>.Handle(AddDropClosed e) {
		var row = Schedule(e.ScheduleId);
		row.IsClosed = true;
		_scheduleWrites.Upsert(row.ScheduleId, row);
	}

	private void Section(Guid sectionId, Action<TimetableSectionRow> change) {
		if (!_sections.TryGetValue(sectionId, out var section))
			_sections[sectionId] = section = new TimetableSectionRow { SectionId = sectionId };
		change(section);
		_sectionWrites.Upsert(sectionId, section);
		// The fan-out: one section event, every entry for that section, one transaction.
		foreach (var id in EntriesOf(_entriesBySection, sectionId))
			Write(_entries[id], entry => Describe(entry, section));
	}

	private void Add(Guid scheduleId, Guid sectionId, string courseCode, int credits, Meeting[] meetings) {
		var schedule = Schedule(scheduleId);
		var entry = new TimetableEntryRow {
			Id = TimetableEntryRow.IdFor(scheduleId, sectionId),
			ScheduleId = scheduleId,
			SectionId = sectionId,
			StudentId = schedule.StudentId,
			Term = schedule.Term,
			CourseCode = courseCode,
			Credits = credits,
			MeetingsJson = MeetingJson.Write(meetings)
		};
		if (_sections.TryGetValue(sectionId, out var section))
			Describe(entry, section);
		Index(entry);
		_entryWrites.Upsert(entry.Id, entry);
		Recount(schedule);
	}

	private void Remove(Guid scheduleId, Guid sectionId) {
		var id = TimetableEntryRow.IdFor(scheduleId, sectionId);
		if (!_entries.Remove(id))
			return;
		_entriesBySection[sectionId].Remove(id);
		_entriesBySchedule[scheduleId].Remove(id);
		_entryWrites.Delete(id);
		Recount(Schedule(scheduleId));
	}

	private void Recount(ScheduleRow schedule) {
		var entries = EntriesOf(_entriesBySchedule, schedule.ScheduleId).Select(id => _entries[id]).ToList();
		schedule.Credits = entries.Sum(e => e.Credits);
		schedule.SectionIds = entries.Select(e => e.SectionId).Order().ToArray();
		_scheduleWrites.Upsert(schedule.ScheduleId, schedule);
	}

	private ScheduleRow Schedule(Guid scheduleId) {
		if (!_schedules.TryGetValue(scheduleId, out var row))
			_schedules[scheduleId] = row = new ScheduleRow { ScheduleId = scheduleId };
		return row;
	}

	// A section first seen through an entry has no title yet; leave the entry's own values until it does.
	private static void Describe(TimetableEntryRow entry, TimetableSectionRow section) {
		if (section.Title.Length == 0)
			return;
		entry.CourseCode = section.CourseCode;
		entry.SectionNumber = section.SectionNumber;
		entry.Title = section.Title;
		entry.MeetingsJson = section.MeetingsJson;
	}

	private void Write(TimetableEntryRow entry, Action<TimetableEntryRow> change) {
		change(entry);
		_entryWrites.Upsert(entry.Id, entry);
	}

	private void Index(TimetableEntryRow entry) {
		_entries[entry.Id] = entry;
		Add(_entriesBySection, entry.SectionId, entry.Id);
		Add(_entriesBySchedule, entry.ScheduleId, entry.Id);

		static void Add(Dictionary<Guid, HashSet<Guid>> index, Guid key, Guid id) {
			if (!index.TryGetValue(key, out var ids))
				index[key] = ids = [];
			ids.Add(id);
		}
	}

	private static IEnumerable<Guid> EntriesOf(Dictionary<Guid, HashSet<Guid>> index, Guid key) =>
		index.TryGetValue(key, out var ids) ? ids : [];
}
