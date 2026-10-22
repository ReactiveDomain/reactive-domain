using Dapper;
using Npgsql;
using ReactiveDomain;

namespace CourseRegistration.ReadModels.Persistence;

/// <summary>The read-side database in Postgres, written through Dapper.</summary>
public sealed class PostgresProjectionDb : IProjectionDb {
	private readonly NpgsqlDataSource _dataSource;

	/// <summary>Uses <paramref name="dataSource"/>; the tables must already exist (see <see cref="ProjectionSchema"/>).</summary>
	public PostgresProjectionDb(NpgsqlDataSource dataSource) {
		_dataSource = dataSource;
		Checkpoints = new PostgresCheckpointStore(dataSource);
	}

	/// <inheritdoc/>
	public ICheckpointStore Checkpoints { get; }

	/// <inheritdoc/>
	public IRowStore<TKey, TRow> Rows<TKey, TRow>() where TKey : notnull where TRow : class, new() =>
		new PostgresRowStore<TKey, TRow>(_dataSource);

	/// <inheritdoc/>
	public void InTransaction(Action<IWriteScope> work) {
		using var connection = _dataSource.OpenConnection();
		using var transaction = connection.BeginTransaction();
		work(new Scope(connection, transaction));
		transaction.Commit();
	}

	internal sealed record Scope(NpgsqlConnection Connection, NpgsqlTransaction Transaction) : IWriteScope;

	internal static Scope Of(IWriteScope scope) =>
		scope as Scope ?? throw new ArgumentException("The scope belongs to a different database.", nameof(scope));
}

/// <summary>One table in Postgres.</summary>
/// <remarks>
/// The SQL is generated from <see cref="RowMap{TRow}"/>, so a row type is its own schema
/// description; the DDL in <see cref="ProjectionSchema"/> must agree with it.
/// </remarks>
public sealed class PostgresRowStore<TKey, TRow> : IRowStore<TKey, TRow> where TKey : notnull where TRow : class, new() {
	private static readonly string Select = $"SELECT {RowMap<TRow>.SelectList} FROM {RowMap<TRow>.Table}";
	private static readonly string ColumnList = string.Join(", ", RowMap<TRow>.Columns.Select(c => c.Name));
	private static readonly string ValueList = string.Join(", ", RowMap<TRow>.Columns.Select(c => c.Parameter));

	private static readonly string InsertSql = $"""
		INSERT INTO {RowMap<TRow>.Table} ({ColumnList})
		VALUES ({ValueList})
		ON CONFLICT ({RowMap<TRow>.Key.Name}) DO NOTHING
		""";

	private static readonly string UpsertSql = $"""
		INSERT INTO {RowMap<TRow>.Table} ({ColumnList})
		VALUES ({ValueList})
		ON CONFLICT ({RowMap<TRow>.Key.Name}) DO UPDATE SET
		{string.Join(",\n", RowMap<TRow>.Columns.Where(c => c != RowMap<TRow>.Key).Select(c => $"{c.Name} = EXCLUDED.{c.Name}"))}
		""";

	private readonly NpgsqlDataSource _dataSource;

	/// <summary>Reads and writes through <paramref name="dataSource"/>.</summary>
	public PostgresRowStore(NpgsqlDataSource dataSource) => _dataSource = dataSource;

	/// <inheritdoc/>
	public TRow? Get(TKey key) {
		using var connection = _dataSource.OpenConnection();
		return connection.QuerySingleOrDefault<TRow>($"{Select} WHERE {RowMap<TRow>.Key.Name} = @Key", new { Key = key });
	}

	/// <inheritdoc/>
	public IReadOnlyList<TRow> All() {
		using var connection = _dataSource.OpenConnection();
		return connection.Query<TRow>(Select).AsList();
	}

	/// <inheritdoc/>
	public IReadOnlyList<TRow> FindBy(params Match[] matches) {
		if (matches.Length == 0)
			return All();
		var parameters = new DynamicParameters();
		var clauses = matches.Select((m, i) => {
			var column = RowMap<TRow>.Column(m.Column).Name;
			parameters.Add($"p{i}", m.Value);
			// UPPER on both sides, so the UNIQUE (UPPER(column)) index serves it. Writing
			// "column = UPPER(@p)" would scan the table and still compare case-sensitively.
			return m.IgnoreCase ? $"UPPER({column}) = UPPER(@p{i})" : $"{column} = @p{i}";
		});
		using var connection = _dataSource.OpenConnection();
		return connection.Query<TRow>($"{Select} WHERE {string.Join(" AND ", clauses)}", parameters).AsList();
	}

	/// <inheritdoc/>
	public TRow? FindOne(params Match[] matches) => RowStoreRules.Single(FindBy(matches));

	/// <inheritdoc/>
	public IReadOnlyList<TRow> FindIn<TValue>(string column, IReadOnlyCollection<TValue> values) {
		if (values.Count == 0)
			return [];
		using var connection = _dataSource.OpenConnection();
		return connection.Query<TRow>(
			$"{Select} WHERE {RowMap<TRow>.Column(column).Name} = ANY(@Values)",
			new { Values = values.ToArray() }).AsList();
	}

	/// <inheritdoc/>
	public Page<TRow> Page(RowFilter<TRow>? filter, IReadOnlyList<OrderBy> order, int offset, int limit) {
		var where = filter is null ? "" : $"WHERE {filter.Sql}";
		var parameters = new DynamicParameters();
		foreach (var (name, value) in filter?.Parameters ?? new Dictionary<string, object?>())
			parameters.Add(name, value);
		parameters.Add("Offset", offset);
		parameters.Add("Limit", limit);

		using var connection = _dataSource.OpenConnection();
		var total = connection.ExecuteScalar<int>($"SELECT COUNT(*) FROM {RowMap<TRow>.Table} {where}", parameters);
		var items = connection.Query<TRow>(
			$"{Select} {where} ORDER BY {OrderSql(order)} OFFSET @Offset LIMIT @Limit", parameters).AsList();
		return new Page<TRow>(items, total);
	}

	/// <inheritdoc/>
	public void Upsert(IWriteScope scope, TRow row) {
		var s = PostgresProjectionDb.Of(scope);
		s.Connection.Execute(UpsertSql, row, s.Transaction);
	}

	/// <inheritdoc/>
	public bool Insert(IWriteScope scope, TRow row) {
		var s = PostgresProjectionDb.Of(scope);
		return s.Connection.Execute(InsertSql, row, s.Transaction) == 1;
	}

	/// <inheritdoc/>
	public void Delete(IWriteScope scope, TKey key) {
		var s = PostgresProjectionDb.Of(scope);
		s.Connection.Execute($"DELETE FROM {RowMap<TRow>.Table} WHERE {RowMap<TRow>.Key.Name} = @Key", new { Key = key }, s.Transaction);
	}

	// COLLATE "C" sorts text by code point, as the in-memory store does; the database's default
	// collation would order mixed case differently and the two stores would page differently.
	private static string OrderSql(IReadOnlyList<OrderBy> order) =>
		string.Join(", ", RowStoreRules.WithKeyLast<TRow>(order).Select(o => {
			var column = RowMap<TRow>.Column(o.Column);
			var collate = column.Property.PropertyType == typeof(string) ? " COLLATE \"C\"" : "";
			return $"{column.Name}{collate} {(o.Descending ? "DESC" : "ASC")}";
		}));
}

/// <summary>Projection checkpoints in the <c>projection_checkpoints</c> table.</summary>
public sealed class PostgresCheckpointStore : ICheckpointStore {
	private readonly NpgsqlDataSource _dataSource;

	/// <summary>Reads through <paramref name="dataSource"/>; writes go through the caller's scope.</summary>
	public PostgresCheckpointStore(NpgsqlDataSource dataSource) => _dataSource = dataSource;

	/// <inheritdoc/>
	public IReadOnlyList<StreamCheckpoint> Load(string projection) {
		using var connection = _dataSource.OpenConnection();
		return connection.Query<(string Stream, long Version)>(
				"SELECT stream_name, version FROM projection_checkpoints WHERE projection = @projection",
				new { projection })
			.Select(r => new StreamCheckpoint(r.Stream, r.Version))
			.ToList();
	}

	/// <inheritdoc/>
	public void Save(IWriteScope scope, string projection, IEnumerable<StreamCheckpoint> checkpoints) {
		var s = PostgresProjectionDb.Of(scope);
		foreach (var checkpoint in checkpoints) {
			if (checkpoint.Version is not { } version)
				continue;
			s.Connection.Execute("""
				INSERT INTO projection_checkpoints (projection, stream_name, version, saved_at)
				VALUES (@projection, @stream, @version, now())
				ON CONFLICT (projection, stream_name) DO UPDATE SET
					version = GREATEST(projection_checkpoints.version, EXCLUDED.version),
					saved_at = EXCLUDED.saved_at
				""", new { projection, stream = checkpoint.StreamName, version }, s.Transaction);
		}
	}
}

internal static class RowStoreRules {
	public static TRow? Single<TRow>(IReadOnlyList<TRow> matches) where TRow : class =>
		matches.Count switch {
			0 => null,
			1 => matches[0],
			_ => throw new InvalidOperationException(
				$"{matches.Count} {typeof(TRow).Name} rows matched where one was expected; a unique index is missing.")
		};

	public static IEnumerable<OrderBy> WithKeyLast<TRow>(IReadOnlyList<OrderBy> order) {
		var key = RowMap<TRow>.Key.Name;
		return order.Any(o => string.Equals(RowMap<TRow>.Column(o.Column).Name, key, StringComparison.Ordinal))
			? order
			: order.Append(new OrderBy(key));
	}
}
