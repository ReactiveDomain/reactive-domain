using Dapper;
using Npgsql;

namespace CourseRegistration.ReadModels;

/// <summary>The read-side tables. Idempotent: safe to run on every start.</summary>
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
