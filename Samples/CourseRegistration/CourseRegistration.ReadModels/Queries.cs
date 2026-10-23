using CourseRegistration.ReadModels.Persistence;

namespace CourseRegistration.ReadModels;

/// <summary>What a student searching for a section asks for. Null fields do not narrow.</summary>
public sealed record SectionSearch(
	string Term,
	string? Department = null,
	int? StartsAtOrAfterMinute = null,
	DayOfWeek[]? AvoidDays = null,
	bool OpenOnly = true);

/// <summary>Reads for the course picker. Takes read-only stores: a query never writes.</summary>
public sealed class SectionQueries {
	private readonly IReadOnlyRowStore<Guid, SectionRow> _sections;

	/// <summary>Reads <c>sections</c> through <paramref name="sections"/>.</summary>
	public SectionQueries(IReadOnlyRowStore<Guid, SectionRow> sections) => _sections = sections;

	/// <summary>The section, or null.</summary>
	public SectionRow? Get(Guid id) => _sections.Get(id);

	/// <summary>Finds a section by its catalog name, e.g. <c>2026FA chem 101 section 2</c>, ignoring the course code's case.</summary>
	public SectionRow? Find(string term, string courseCode, int sectionNumber) =>
		_sections.FindOne(
			Match.Eq("term", term),
			Match.IgnoringCase("course_code", courseCode),
			Match.Eq("section_number", sectionNumber));

	/// <summary>The sections with the given ids, in no particular order.</summary>
	public IReadOnlyList<SectionRow> GetMany(IReadOnlyCollection<Guid> ids) => _sections.FindIn("id", ids);

	/// <summary>One page of matching sections, by course code and then section number.</summary>
	public Page<SectionRow> Search(SectionSearch search, int offset, int limit) =>
		_sections.Page(Filter(search), new OrderBy("course_code").Then("section_number"), offset, limit);

	/// <summary>The filter <see cref="Search"/> applies: SQL for Postgres, the same rule as a predicate for memory.</summary>
	public static RowFilter<SectionRow> Filter(SectionSearch search) {
		var filter = new RowFilter<SectionRow>(
			"term = @Term AND NOT is_cancelled",
			new Dictionary<string, object?> { ["Term"] = search.Term },
			r => r.Term == search.Term && !r.IsCancelled);

		if (search.Department is { } department) {
			filter = filter.And(new RowFilter<SectionRow>(
				"department = @Department",
				new Dictionary<string, object?> { ["Department"] = department.Trim().ToUpperInvariant() },
				r => r.Department == department.Trim().ToUpperInvariant()));
		}

		if (search.StartsAtOrAfterMinute is { } minute) {
			// Every meeting starts at or after the time: a student blocking mornings wants no early meeting at all.
			filter = filter.And(new RowFilter<SectionRow>(
				"NOT EXISTS (SELECT 1 FROM jsonb_array_elements(meetings) m WHERE (m->>'StartMinute')::int < @Minute)",
				new Dictionary<string, object?> { ["Minute"] = minute },
				r => r.Meetings.All(m => m.StartMinute >= minute)));
		}

		if (search.AvoidDays is { Length: > 0 } avoid) {
			// && is array overlap; the GIN index on meeting_days serves it.
			var days = avoid.Select(d => (int)d).ToArray();
			filter = filter.And(new RowFilter<SectionRow>(
				"NOT (meeting_days && @AvoidDays)",
				new Dictionary<string, object?> { ["AvoidDays"] = days },
				r => !r.MeetingDays.Intersect(days).Any()));
		}

		if (search.OpenOnly) {
			filter = filter.And(new RowFilter<SectionRow>(
				"seats_held < capacity",
				new Dictionary<string, object?>(),
				r => r.SeatsHeld < r.Capacity));
		}

		return filter;
	}
}

/// <summary>Reads for a student's timetable and a section's roster.</summary>
public sealed class TimetableQueries {
	private readonly IReadOnlyRowStore<Guid, ScheduleRow> _schedules;
	private readonly IReadOnlyRowStore<Guid, TimetableEntryRow> _entries;

	/// <summary>Reads <c>schedules</c> and <c>timetable_entries</c>.</summary>
	public TimetableQueries(IReadOnlyRowStore<Guid, ScheduleRow> schedules, IReadOnlyRowStore<Guid, TimetableEntryRow> entries) {
		_schedules = schedules;
		_entries = entries;
	}

	/// <summary>The schedule, or null.</summary>
	public ScheduleRow? Schedule(Guid scheduleId) => _schedules.Get(scheduleId);

	/// <summary>The courses on a schedule, by course code.</summary>
	public IReadOnlyList<TimetableEntryRow> Timetable(Guid scheduleId) =>
		_entries.FindBy(Match.Eq("schedule_id", scheduleId)).OrderBy(e => e.CourseCode, StringComparer.Ordinal).ToList();

	/// <summary>The students enrolled in a section.</summary>
	public IReadOnlyList<Guid> Roster(Guid sectionId) =>
		_entries.FindBy(Match.Eq("section_id", sectionId)).Select(e => e.StudentId).ToList();

	/// <summary>The schedules that include <paramref name="sectionId"/>, read from the schedules' own array column.</summary>
	public IReadOnlyList<ScheduleRow> SchedulesWith(Guid sectionId) =>
		_schedules.Find(new RowFilter<ScheduleRow>(
			"@Section = ANY(section_ids)",
			new Dictionary<string, object?> { ["Section"] = sectionId },
			r => r.SectionIds.Contains(sectionId)));

	/// <summary>The schedules in a term whose add/drop is still open.</summary>
	public IReadOnlyList<ScheduleRow> OpenSchedules(string term) =>
		_schedules.FindBy(Match.Eq("term", term), Match.Eq("is_closed", false));
}
