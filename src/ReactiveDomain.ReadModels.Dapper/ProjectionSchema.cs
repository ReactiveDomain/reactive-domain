using Dapper;
using Npgsql;

namespace ReactiveDomain.ReadModels;

/// <summary>The tables the projections themselves need. Idempotent: safe to run on every start.</summary>
/// <remarks>
/// Only the bookkeeping tables are created here — checkpoints, writer fences and reset keys. The row
/// tables the projections fill are the application's own DDL, written to agree with each row type's
/// <see cref="RowMap{TRow}"/>.
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

		CREATE TABLE IF NOT EXISTS projection_writers (
			projection TEXT        PRIMARY KEY,
			writer     UUID        NOT NULL,
			claimed_at TIMESTAMPTZ NOT NULL
		);

		-- Which reset keys have been applied; see ProjectionReset.
		CREATE TABLE IF NOT EXISTS projection_resets (
			reset_key TEXT        PRIMARY KEY,
			reset_at  TIMESTAMPTZ NOT NULL
		);
		""";

	/// <summary>Creates any bookkeeping table that does not exist yet.</summary>
	public static void Create(NpgsqlDataSource dataSource) {
		using var connection = dataSource.OpenConnection();
		connection.Execute(Sql);
	}
}
