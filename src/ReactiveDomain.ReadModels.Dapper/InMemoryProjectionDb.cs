namespace ReactiveDomain.ReadModels;

/// <summary>
/// The read-side database held in memory, for tests. It keeps the Postgres store's contract,
/// transactions included: writes land only when the work returns, so work that throws leaves nothing
/// behind, and no read sees an uncommitted write — as in Postgres, where a read opens its own
/// connection — except a scoped read, which sees its own transaction's.
/// </summary>
/// <remarks>
/// Filters run their predicate half and ignore the SQL, and no unique index is enforced. Run the same
/// tests against <see cref="PostgresProjectionDb"/> to cover what this cannot.
/// </remarks>
public sealed class InMemoryProjectionDb : IProjectionDb {
	private readonly object _gate = new();
	private readonly Dictionary<Type, object> _tables = [];
	private readonly InMemoryCheckpointStore _checkpoints;

	/// <summary>Creates an empty database.</summary>
	public InMemoryProjectionDb() {
		_checkpoints = new InMemoryCheckpointStore(_gate);
		Writers = new InMemoryProjectionWriters(_gate);
	}

	/// <inheritdoc/>
	public ICheckpointStore Checkpoints => _checkpoints;

	/// <inheritdoc/>
	public IProjectionWriters Writers { get; }

	/// <inheritdoc/>
	public IRowStore<TKey, TRow> Rows<TKey, TRow>() where TKey : notnull where TRow : class, new() {
		lock (_gate) {
			if (!_tables.TryGetValue(typeof(TRow), out var table))
				_tables[typeof(TRow)] = table = new InMemoryRowStore<TKey, TRow>(_gate);
			return (IRowStore<TKey, TRow>)table;
		}
	}

	/// <inheritdoc/>
	public void InTransaction(Action<IWriteScope> work) {
		lock (_gate) {
			var scope = new Scope();
			work(scope);
			scope.Commit();
		}
	}

	/// <summary>Holds each store's pending writes until the work returns; a throw discards them all.</summary>
	internal sealed class Scope : IWriteScope {
		private readonly Dictionary<object, object> _pending = [];
		private readonly List<Action> _commits = [];

		public TPending PendingFor<TPending>(object store, Action<TPending> commit) where TPending : class, new() {
			if (_pending.TryGetValue(store, out var existing))
				return (TPending)existing;
			var pending = new TPending();
			_pending[store] = pending;
			_commits.Add(() => commit(pending));
			return pending;
		}

		public void Commit() {
			foreach (var commit in _commits)
				commit();
		}
	}

	internal static Scope Of(IWriteScope scope) =>
		scope as Scope ?? throw new ArgumentException("The scope belongs to a different database.", nameof(scope));
}

/// <summary>One table in memory. Rows are copied in and out, so a caller never holds a stored row.</summary>
public sealed class InMemoryRowStore<TKey, TRow> : IRowStore<TKey, TRow> where TKey : notnull where TRow : class, new() {
	private readonly object _gate;
	private readonly Dictionary<TKey, TRow> _rows = [];

	internal InMemoryRowStore(object gate) => _gate = gate;

	/// <inheritdoc/>
	public TRow? Get(TKey key) {
		lock (_gate)
			return _rows.TryGetValue(key, out var row) ? RowMap<TRow>.Copy(row) : null;
	}

	/// <inheritdoc/>
	public TRow? Get(IWriteScope scope, TKey key) {
		lock (_gate) {
			if (Pending(scope).TryGetValue(key, out var pending))
				return pending is null ? null : RowMap<TRow>.Copy(pending);
			return Get(key);
		}
	}

	/// <inheritdoc/>
	public IReadOnlyList<TRow> All() => Where(_ => true);

	/// <inheritdoc/>
	public IReadOnlyList<TRow> FindBy(params Match[] matches) {
		// Resolved before any row is read, so an unknown column throws on an empty table too, as SQL does.
		foreach (var match in matches)
			RowMap<TRow>.Column(match.Column);
		return Where(row => matches.All(m => Matches(row, m)));
	}

	/// <inheritdoc/>
	public TRow? FindOne(params Match[] matches) => RowStoreRules.Single(FindBy(matches));

	/// <inheritdoc/>
	public IReadOnlyList<TRow> FindIn<TValue>(string column, IReadOnlyCollection<TValue> values) {
		var property = RowMap<TRow>.Column(column).Property;
		var wanted = values.Cast<object?>().ToHashSet();
		return Where(row => wanted.Contains(property.GetValue(row)));
	}

	/// <inheritdoc/>
	public IReadOnlyList<TRow> Find(RowFilter<TRow> filter) => Where(filter.Predicate);

	/// <inheritdoc/>
	public Page<TRow> Page(RowFilter<TRow>? filter, IReadOnlyList<OrderBy> order, int offset, int limit) {
		var kept = Where(filter?.Predicate ?? (_ => true));
		IOrderedEnumerable<TRow>? sorted = null;
		foreach (var o in RowStoreRules.WithKeyLast<TRow>(order)) {
			Func<TRow, object?> key = row => RowMap<TRow>.Value(row, o.Column);
			sorted = sorted is null
				? o.Descending ? kept.OrderByDescending(key, Comparer) : kept.OrderBy(key, Comparer)
				: o.Descending ? sorted.ThenByDescending(key, Comparer) : sorted.ThenBy(key, Comparer);
		}
		return new Page<TRow>((sorted ?? kept.AsEnumerable()).Skip(offset).Take(limit).ToList(), kept.Count);
	}

	/// <inheritdoc/>
	public void Upsert(IWriteScope scope, TRow row) {
		lock (_gate)
			Pending(scope)[KeyOf(row)] = RowMap<TRow>.Copy(row);
	}

	/// <inheritdoc/>
	public bool Insert(IWriteScope scope, TRow row) {
		var key = KeyOf(row);
		lock (_gate) {
			var pending = Pending(scope);
			var taken = pending.TryGetValue(key, out var p) ? p is not null : _rows.ContainsKey(key);
			if (taken)
				return false;
			pending[key] = RowMap<TRow>.Copy(row);
			return true;
		}
	}

	/// <inheritdoc/>
	public void Delete(IWriteScope scope, TKey key) {
		lock (_gate)
			Pending(scope)[key] = null;
	}

	// A null row is a delete. The pending writes land in Apply when the transaction commits.
	private Dictionary<TKey, TRow?> Pending(IWriteScope scope) =>
		InMemoryProjectionDb.Of(scope).PendingFor<Dictionary<TKey, TRow?>>(this, Apply);

	private void Apply(Dictionary<TKey, TRow?> pending) {
		foreach (var (key, row) in pending) {
			if (row is null)
				_rows.Remove(key);
			else
				_rows[key] = row;
		}
	}

	private List<TRow> Where(Func<TRow, bool> keep) {
		lock (_gate)
			return _rows.Values.Where(keep).Select(RowMap<TRow>.Copy).ToList();
	}

	private static TKey KeyOf(TRow row) => (TKey)RowMap<TRow>.Key.Property.GetValue(row)!;

	private static bool Matches(TRow row, Match match) {
		var actual = RowMap<TRow>.Value(row, match.Column);
		return match.IgnoreCase
			? string.Equals(actual as string, match.Value as string, StringComparison.OrdinalIgnoreCase)
			: Equals(actual, match.Value);
	}

	private static readonly IComparer<object?> Comparer = Comparer<object?>.Create((a, b) =>
		a is string x && b is string y ? string.CompareOrdinal(x, y) : Comparer<object?>.Default.Compare(a, b));
}

// The gate serializes claims against whole transactions, as the row lock does in Postgres.
internal sealed class InMemoryProjectionWriters(object gate) : IProjectionWriters {
	private readonly Dictionary<string, Guid> _writers = [];

	public void Claim(string projection, Guid writer) {
		lock (gate)
			_writers[projection] = writer;
	}

	public void Assert(IWriteScope scope, string projection, Guid writer) {
		InMemoryProjectionDb.Of(scope);
		lock (gate) {
			if (!_writers.TryGetValue(projection, out var current) || current != writer)
				throw new FencedWriterException(projection);
		}
	}
}

internal sealed class InMemoryCheckpointStore(object gate) : ICheckpointStore {
	private readonly Dictionary<(string Projection, string Stream), long> _versions = [];

	public IReadOnlyList<StreamCheckpoint> Load(string projection) {
		lock (gate)
			return _versions.Where(v => v.Key.Projection == projection)
				.Select(v => new StreamCheckpoint(v.Key.Stream, v.Value))
				.ToList();
	}

	public void Save(IWriteScope scope, string projection, IEnumerable<StreamCheckpoint> checkpoints) {
		var pending = InMemoryProjectionDb.Of(scope).PendingFor<Dictionary<(string, string), long>>(this, Apply);
		lock (gate) {
			foreach (var checkpoint in checkpoints) {
				if (checkpoint.Version is not { } version)
					continue;
				var key = (projection, checkpoint.StreamName);
				var current = pending.TryGetValue(key, out var p) ? p : _versions.TryGetValue(key, out var v) ? v : -1;
				if (current < version)
					pending[key] = version;
			}
		}
	}

	private void Apply(Dictionary<(string, string), long> pending) {
		foreach (var (key, version) in pending)
			if (!_versions.TryGetValue(key, out var current) || current < version)
				_versions[key] = version;
	}
}
