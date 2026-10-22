using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Security.Cryptography;
using System.Text.Json;
using CourseRegistration.Domain;

namespace CourseRegistration.ReadModels;

/// <summary>Meeting lists stored as <c>jsonb</c>.</summary>
public static class MeetingJson {
	/// <summary>The meetings as a JSON array.</summary>
	public static string Write(IEnumerable<Meeting> meetings) => JsonSerializer.Serialize(meetings.ToArray());

	/// <summary>The meetings in <paramref name="json"/>; none for an empty column.</summary>
	public static Meeting[] Read(string? json) =>
		string.IsNullOrEmpty(json) ? [] : JsonSerializer.Deserialize<Meeting[]>(json) ?? [];
}

/// <summary>A section as the course search shows it. Owned by <see cref="SectionCatalogRM"/>.</summary>
[Table("sections")]
public sealed class SectionRow {
	[Key] public Guid Id { get; set; }
	public string Term { get; set; } = string.Empty;
	public string CourseCode { get; set; } = string.Empty;
	public int SectionNumber { get; set; }
	public string Title { get; set; } = string.Empty;
	public int Credits { get; set; }
	public int Capacity { get; set; }
	public int SeatsHeld { get; set; }
	public bool IsCancelled { get; set; }
	[Column("meetings", TypeName = "jsonb")] public string MeetingsJson { get; set; } = "[]";

	[NotMapped] public Meeting[] Meetings { get => MeetingJson.Read(MeetingsJson); set => MeetingsJson = MeetingJson.Write(value); }
	[NotMapped] public int SeatsOpen => Math.Max(0, Capacity - SeatsHeld);
}

/// <summary>What the timetable shows of a section. Owned by <see cref="StudentTimetableRM"/>.</summary>
/// <remarks>
/// A copy rather than a read of <see cref="SectionRow"/>: the timetable resumes from its own
/// checkpoints, so it cannot depend on how far another model has got.
/// </remarks>
[Table("timetable_sections")]
public sealed class TimetableSectionRow {
	[Key] public Guid SectionId { get; set; }
	public string CourseCode { get; set; } = string.Empty;
	public int SectionNumber { get; set; }
	public string Title { get; set; } = string.Empty;
	[Column("meetings", TypeName = "jsonb")] public string MeetingsJson { get; set; } = "[]";
}

/// <summary>A student's schedule for a term. Owned by <see cref="StudentTimetableRM"/>.</summary>
[Table("schedules")]
public sealed class ScheduleRow {
	[Key] public Guid ScheduleId { get; set; }
	public Guid StudentId { get; set; }
	public string Term { get; set; } = string.Empty;
	public int Credits { get; set; }
	public bool IsClosed { get; set; }
}

/// <summary>One course on a student's timetable. Owned by <see cref="StudentTimetableRM"/>.</summary>
[Table("timetable_entries")]
public sealed class TimetableEntryRow {
	[Key] public Guid Id { get; set; }
	public Guid ScheduleId { get; set; }
	public Guid SectionId { get; set; }
	public Guid StudentId { get; set; }
	public string Term { get; set; } = string.Empty;
	public string CourseCode { get; set; } = string.Empty;
	public int SectionNumber { get; set; }
	public string Title { get; set; } = string.Empty;
	public int Credits { get; set; }
	[Column("meetings", TypeName = "jsonb")] public string MeetingsJson { get; set; } = "[]";

	[NotMapped] public Meeting[] Meetings => MeetingJson.Read(MeetingsJson);

	/// <summary>The entry's key, derived from the schedule and section so a replay lands on the same row.</summary>
	public static Guid IdFor(Guid scheduleId, Guid sectionId) =>
		new(MD5.HashData([.. scheduleId.ToByteArray(), .. sectionId.ToByteArray()]));
}
