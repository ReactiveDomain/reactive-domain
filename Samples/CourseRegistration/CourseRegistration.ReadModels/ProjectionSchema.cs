using Dapper;
using Npgsql;

namespace CourseRegistration.ReadModels;

/// <summary>The read-side tables. Idempotent: safe to run on every start.</summary>
/// <remarks>
/// The tables are created in their first shape and every later change is a step after that, so a new
/// database and an upgraded one end up identical. A change a backfill cannot express (a column computed
/// from events no row kept) needs the projection rebuilt instead; see <see cref="ProjectionReset"/>.
/// </remarks>
public static class ProjectionSchema {
	private const string Sql = """
		CREATE TABLE IF NOT EXISTS projection_checkpoints (
			projection  TEXT        NOT NULL,
			stream_name TEXT        NOT NULL,
			version     BIGINT      NOT NULL,
			saved_at    TIMESTAMPTZ NOT NULL,
			PRIMARY KEY (projection, stream_name)
		);

		CREATE TABLE IF NOT EXISTS sections (
			id             UUID    PRIMARY KEY,
			term           TEXT    NOT NULL,
			course_code    TEXT    NOT NULL,
			section_number INTEGER NOT NULL,
			title          TEXT    NOT NULL,
			credits        INTEGER NOT NULL,
			capacity       INTEGER NOT NULL,
			seats_held     INTEGER NOT NULL,
			is_cancelled   BOOLEAN NOT NULL,
			meetings       JSONB   NOT NULL
		);
		-- The natural key. An expression index, so course_code keeps its display casing while a
		-- lookup by UPPER(course_code) = UPPER(@p) still uses the index.
		CREATE UNIQUE INDEX IF NOT EXISTS sections_natural_key
			ON sections (term, UPPER(course_code), section_number);

		CREATE TABLE IF NOT EXISTS timetable_sections (
			section_id     UUID    PRIMARY KEY,
			course_code    TEXT    NOT NULL,
			section_number INTEGER NOT NULL,
			title          TEXT    NOT NULL,
			meetings       JSONB   NOT NULL
		);

		CREATE TABLE IF NOT EXISTS schedules (
			schedule_id UUID    PRIMARY KEY,
			student_id  UUID    NOT NULL,
			term        TEXT    NOT NULL,
			credits     INTEGER NOT NULL,
			is_closed   BOOLEAN NOT NULL
		);

		CREATE TABLE IF NOT EXISTS timetable_entries (
			id             UUID    PRIMARY KEY,
			schedule_id    UUID    NOT NULL,
			section_id     UUID    NOT NULL,
			student_id     UUID    NOT NULL,
			term           TEXT    NOT NULL,
			course_code    TEXT    NOT NULL,
			section_number INTEGER NOT NULL,
			title          TEXT    NOT NULL,
			credits        INTEGER NOT NULL,
			meetings       JSONB   NOT NULL,
			UNIQUE (schedule_id, section_id)
		);
		CREATE INDEX IF NOT EXISTS timetable_entries_section ON timetable_entries (section_id);

		-- v2: columns added after the first release. Each comes in with a default, then is backfilled
		-- from what the row already holds, so an existing database upgrades in place with no rebuild.
		-- Re-running is a no-op: the columns exist and every backfill skips rows it has filled.
		ALTER TABLE sections ADD COLUMN IF NOT EXISTS department TEXT NOT NULL DEFAULT '';
		UPDATE sections SET department = UPPER(split_part(course_code, ' ', 1)) WHERE department = '';

		ALTER TABLE sections ADD COLUMN IF NOT EXISTS meeting_days INTEGER[] NOT NULL DEFAULT '{}';
		UPDATE sections
		SET meeting_days = ARRAY(SELECT DISTINCT (m->>'Day')::int FROM jsonb_array_elements(meetings) m ORDER BY 1)
		WHERE cardinality(meeting_days) = 0 AND jsonb_array_length(meetings) > 0;
		CREATE INDEX IF NOT EXISTS sections_meeting_days ON sections USING GIN (meeting_days);

		ALTER TABLE schedules ADD COLUMN IF NOT EXISTS section_ids UUID[] NOT NULL DEFAULT '{}';
		UPDATE schedules s
		SET section_ids = ARRAY(SELECT e.section_id FROM timetable_entries e WHERE e.schedule_id = s.schedule_id ORDER BY e.section_id)
		WHERE cardinality(s.section_ids) = 0
		  AND EXISTS (SELECT 1 FROM timetable_entries e WHERE e.schedule_id = s.schedule_id);
		CREATE INDEX IF NOT EXISTS schedules_section_ids ON schedules USING GIN (section_ids);

		-- Which reset keys have been applied; see ProjectionReset.
		CREATE TABLE IF NOT EXISTS projection_resets (
			reset_key TEXT        PRIMARY KEY,
			reset_at  TIMESTAMPTZ NOT NULL
		);

		-- Not projections: written directly, and idempotent by their natural keys.
		CREATE TABLE IF NOT EXISTS reminder_sends (
			student_id UUID        NOT NULL,
			term       TEXT        NOT NULL,
			kind       TEXT        NOT NULL,
			sent_to    TEXT        NOT NULL,
			sent_at    TIMESTAMPTZ NOT NULL,
			PRIMARY KEY (student_id, term, kind)
		);

		CREATE TABLE IF NOT EXISTS command_audit (
			msg_id         UUID        PRIMARY KEY,
			correlation_id UUID        NOT NULL,
			causation_id   UUID        NOT NULL,
			command_type   TEXT        NOT NULL,
			body           JSONB       NOT NULL,
			recorded_at    TIMESTAMPTZ NOT NULL
		);
		CREATE INDEX IF NOT EXISTS command_audit_correlation ON command_audit (correlation_id, recorded_at);

		CREATE TABLE IF NOT EXISTS job_runs (
			job_name     TEXT        NOT NULL,
			window_start TIMESTAMPTZ NOT NULL,
			claimed_by   TEXT        NOT NULL,
			claimed_at   TIMESTAMPTZ NOT NULL,
			lease_until  TIMESTAMPTZ NOT NULL,
			completed_at TIMESTAMPTZ,
			PRIMARY KEY (job_name, window_start)
		);
		""";

	/// <summary>Creates any table or index that does not exist yet.</summary>
	public static void Create(NpgsqlDataSource dataSource) {
		using var connection = dataSource.OpenConnection();
		connection.Execute(Sql);
	}
}
