using ReactiveDomain;

namespace CourseRegistration.ReadModels.Persistence;

/// <summary>
/// The read-side database held in memory, for tests. It keeps the Postgres store's contract,
/// transactions included: work that throws leaves nothing behind.
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
	public InMemoryProjectionDb() => _checkpoints = new InMemoryCheckpointStore(_gate);

	/// <inheritdoc/>
	public ICheckpointStore Checkpoints => _checkpoints;

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
			try {
				work(scope);
			} catch {
				scope.Rollback();
				throw;
			}
		}
	}

	internal sealed class Scope : IWriteScope {
		private readonly Stack<Action> _undo = new();

		public void OnRollback(Action undo) => _undo.Push(undo);

		public void Rollback() {
			while (_undo.Count > 0)
				_undo.Pop()();
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
	public IReadOnlyList<TRow> All() => Where(_ => true);

	/// <inheritdoc/>
	public IReadOnlyList<TRow> FindBy(params Match[] matches) {
		// Resolved before any row is read, so an unknown column fails on an empty table too, as SQL does.
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
		var s = InMemoryProjectionDb.Of(scope);
		var key = KeyOf(row);
		lock (_gate) {
			var had = _rows.TryGetValue(key, out var previous);
			s.OnRollback(() => {
				if (had)
					_rows[key] = previous!;
				else
					_rows.Remove(key);
			});
			_rows[key] = RowMap<TRow>.Copy(row);
		}
	}

	/// <inheritdoc/>
	public bool Insert(IWriteScope scope, TRow row) {
		var s = InMemoryProjectionDb.Of(scope);
		var key = KeyOf(row);
		lock (_gate) {
			if (!_rows.TryAdd(key, RowMap<TRow>.Copy(row)))
				return false;
			s.OnRollback(() => _rows.Remove(key));
			return true;
		}
	}

	/// <inheritdoc/>
	public void Delete(IWriteScope scope, TKey key) {
		var s = InMemoryProjectionDb.Of(scope);
		lock (_gate) {
			if (_rows.Remove(key, out var previous))
				s.OnRollback(() => _rows[key] = previous);
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

internal sealed class InMemoryCheckpointStore(object gate) : ICheckpointStore {
	private readonly Dictionary<(string Projection, string Stream), long> _versions = [];

	public IReadOnlyList<StreamCheckpoint> Load(string projection) {
		lock (gate)
			return _versions.Where(v => v.Key.Projection == projection)
				.Select(v => new StreamCheckpoint(v.Key.Stream, v.Value))
				.ToList();
	}

	public void Save(IWriteScope scope, string projection, IEnumerable<StreamCheckpoint> checkpoints) {
		var s = InMemoryProjectionDb.Of(scope);
		lock (gate) {
			foreach (var checkpoint in checkpoints) {
				if (checkpoint.Version is not { } version)
					continue;
				var key = (projection, checkpoint.StreamName);
				var had = _versions.TryGetValue(key, out var previous);
				if (had && previous >= version)
					continue;
				s.OnRollback(() => {
					if (had)
						_versions[key] = previous;
					else
						_versions.Remove(key);
				});
				_versions[key] = version;
			}
		}
	}
}
