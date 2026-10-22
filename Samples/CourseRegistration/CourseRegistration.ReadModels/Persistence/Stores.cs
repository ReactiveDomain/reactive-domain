using ReactiveDomain;

namespace CourseRegistration.ReadModels.Persistence;

/// <summary>One transaction. Every write takes one, so no row is written apart from its checkpoint.</summary>
public interface IWriteScope { }

/// <summary>An equality condition on one column, optionally ignoring case.</summary>
public readonly record struct Match(string Column, object? Value, bool IgnoreCase = false) {
	/// <summary><c>column = value</c>.</summary>
	public static Match Eq(string column, object? value) => new(column, value);

	/// <summary><c>UPPER(column) = UPPER(value)</c>, which a matching expression index serves.</summary>
	public static Match IgnoringCase(string column, string value) => new(column, value, IgnoreCase: true);
}

/// <summary>
/// A filter written twice: as SQL for Postgres and as a predicate for the in-memory store. Each store
/// uses its own half, so a test against the in-memory store enforces the same rule the SQL does.
/// </summary>
/// <param name="Sql">A WHERE clause without the keyword, naming parameters as <c>@Name</c>.</param>
/// <param name="Parameters">The values for <paramref name="Sql"/>, by name.</param>
/// <param name="Predicate">The same rule over a row.</param>
public sealed record RowFilter<TRow>(string Sql, IReadOnlyDictionary<string, object?> Parameters, Func<TRow, bool> Predicate) {
	/// <summary>Both filters must hold. Parameter names must not repeat between them.</summary>
	public RowFilter<TRow> And(RowFilter<TRow> other) {
		var parameters = new Dictionary<string, object?>(Parameters);
		foreach (var (name, value) in other.Parameters)
			parameters.Add(name, value);
		return new($"({Sql}) AND ({other.Sql})", parameters, row => Predicate(row) && other.Predicate(row));
	}
}

/// <summary>Sort order by column. Text sorts by code point in both stores, so pages agree.</summary>
public sealed record OrderBy(string Column, bool Descending = false) {
	/// <summary>Sorts by this, then by <paramref name="next"/>.</summary>
	public IReadOnlyList<OrderBy> Then(string next, bool descending = false) => [this, new OrderBy(next, descending)];
}

/// <summary>A page of rows, and how many rows matched in all.</summary>
public sealed record Page<TRow>(IReadOnlyList<TRow> Items, int Total);

/// <summary>Reads one table. A read model takes this for any table it does not write.</summary>
public interface IReadOnlyRowStore<TKey, TRow> where TKey : notnull where TRow : class, new() {
	/// <summary>The row with <paramref name="key"/>, or null.</summary>
	TRow? Get(TKey key);

	/// <summary>Every row.</summary>
	IReadOnlyList<TRow> All();

	/// <summary>The rows matching every condition. No conditions matches every row.</summary>
	IReadOnlyList<TRow> FindBy(params Match[] matches);

	/// <summary>The one row matching every condition, or null.</summary>
	/// <exception cref="InvalidOperationException">More than one row matched: the unique index that should prevent it is missing.</exception>
	TRow? FindOne(params Match[] matches);

	/// <summary>The rows whose <paramref name="column"/> holds any of <paramref name="values"/>.</summary>
	/// <remarks>Keep <typeparamref name="TValue"/> the column's own type, so Postgres can type the array parameter.</remarks>
	IReadOnlyList<TRow> FindIn<TValue>(string column, IReadOnlyCollection<TValue> values);

	/// <summary>One page of the rows <paramref name="filter"/> keeps, and how many it keeps in all.</summary>
	/// <remarks>The key always breaks ties last, so a page boundary never splits rows that sort alike.</remarks>
	Page<TRow> Page(RowFilter<TRow>? filter, IReadOnlyList<OrderBy> order, int offset, int limit);
}

/// <summary>Reads and writes one table. Only the read model that owns the table takes this.</summary>
public interface IRowStore<TKey, TRow> : IReadOnlyRowStore<TKey, TRow> where TKey : notnull where TRow : class, new() {
	/// <summary>Writes <paramref name="row"/>, replacing any row with its key.</summary>
	void Upsert(IWriteScope scope, TRow row);

	/// <summary>Writes <paramref name="row"/> unless its key is taken.</summary>
	/// <returns>True when the row was written; false when one with its key was already there.</returns>
	bool Insert(IWriteScope scope, TRow row);

	/// <summary>Removes the row with <paramref name="key"/>, if there is one.</summary>
	void Delete(IWriteScope scope, TKey key);
}

/// <summary>Where each stream of a projection stands, saved beside its rows.</summary>
public interface ICheckpointStore {
	/// <summary>The saved checkpoints of <paramref name="projection"/>, one per stream.</summary>
	IReadOnlyList<StreamCheckpoint> Load(string projection);

	/// <summary>Saves <paramref name="checkpoints"/> in <paramref name="scope"/>'s transaction.</summary>
	/// <remarks>
	/// A checkpoint with no version is skipped: saving 0 would claim the stream's first event. A saved
	/// version never moves backwards, so a stale save cannot make a restart replay what was counted.
	/// </remarks>
	void Save(IWriteScope scope, string projection, IEnumerable<StreamCheckpoint> checkpoints);
}

/// <summary>The read-side database: its tables, its checkpoints, and transactions over both.</summary>
public interface IProjectionDb {
	/// <summary>The store for <typeparamref name="TRow"/>'s table.</summary>
	IRowStore<TKey, TRow> Rows<TKey, TRow>() where TKey : notnull where TRow : class, new();

	/// <summary>The projection checkpoints.</summary>
	ICheckpointStore Checkpoints { get; }

	/// <summary>Runs <paramref name="work"/> in one transaction: it commits if the work returns, and nothing is kept if it throws.</summary>
	void InTransaction(Action<IWriteScope> work);
}

/// <summary>Read-only views, for a read model or query that must not write a table.</summary>
public static class ProjectionDbExtensions {
	/// <summary>The store for <typeparamref name="TRow"/>'s table, typed so it cannot write.</summary>
	public static IReadOnlyRowStore<TKey, TRow> ReadOnlyRows<TKey, TRow>(this IProjectionDb db)
		where TKey : notnull where TRow : class, new() => db.Rows<TKey, TRow>();
}
