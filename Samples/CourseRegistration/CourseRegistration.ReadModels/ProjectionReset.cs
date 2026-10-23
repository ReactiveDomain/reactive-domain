using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using Dapper;
using Npgsql;

namespace CourseRegistration.ReadModels;

/// <summary>
/// Empties projection tables and their checkpoints, so the projections rebuild from the streams on
/// their next start.
/// </summary>
/// <remarks>
/// <para>Run it before any projection starts. A running projection keeps its rows in memory and would
/// write them back.</para>
/// <para>Only tables a projection owns are touched. Ledgers, the command audit and job claims are not
/// projected from events, so nothing could rebuild them.</para>
/// </remarks>
public static class ProjectionReset {
	/// <summary>
	/// Rebuilds every projection in <paramref name="projections"/>, once per distinct
	/// <paramref name="resetKey"/>. A key already applied does nothing.
	/// </summary>
	/// <returns>True when this call claimed the key and reset the tables.</returns>
	/// <remarks>
	/// Meant for a deploy setting: set a new key to rebuild after a projection's handling changes. The
	/// key is claimed in the same transaction as the reset, so when several instances start with it,
	/// one resets and the others, blocked on the key until it commits, find it taken.
	/// </remarks>
	public static bool ResetOnce(NpgsqlDataSource dataSource, string resetKey, params Type[] projections) {
		ArgumentException.ThrowIfNullOrWhiteSpace(resetKey);
		using var connection = dataSource.OpenConnection();
		using var transaction = connection.BeginTransaction();
		var claimed = connection.ExecuteScalar<int?>(
			"INSERT INTO projection_resets (reset_key, reset_at) VALUES (@resetKey, now()) ON CONFLICT DO NOTHING RETURNING 1",
			new { resetKey }, transaction) is not null;
		if (!claimed)
			return false;
		Empty(connection, transaction, projections);
		transaction.Commit();
		return true;
	}

	/// <summary>Rebuilds <paramref name="projection"/> alone: its tables and checkpoints are emptied now.</summary>
	public static void Rebuild(NpgsqlDataSource dataSource, Type projection) {
		using var connection = dataSource.OpenConnection();
		using var transaction = connection.BeginTransaction();
		Empty(connection, transaction, [projection]);
		transaction.Commit();
	}

	// A projection's checkpoints are saved under its name, which by convention is its type's name.
	private static void Empty(NpgsqlConnection connection, NpgsqlTransaction transaction, IReadOnlyCollection<Type> projections) {
		var tables = projections.SelectMany(ProjectionTables.Owned).Select(TableOf).Distinct().ToList();
		if (tables.Count > 0)
			connection.Execute($"TRUNCATE {string.Join(", ", tables)}", transaction: transaction);
		connection.Execute("DELETE FROM projection_checkpoints WHERE projection = ANY(@names)",
			new { names = projections.Select(p => p.Name).ToArray() }, transaction);
	}

	private static string TableOf(Type row) =>
		row.GetCustomAttribute<TableAttribute>()?.Name
		?? throw new InvalidOperationException($"{row.Name} has no [Table] attribute.");
}
