// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation;

/// <summary>What <see cref="BufferedReadModelBase"/> needs from a buffer without knowing its types.</summary>
internal interface IWriteBuffer {
	bool HasPending { get; }
	void Clear();
}

/// <summary>
/// The rows a <see cref="BufferedReadModelBase"/> has changed and not yet written: upserts keyed by
/// id, and deletes. Last write wins per key.
/// </summary>
/// <remarks>
/// <para>Not thread-safe, and does not need to be: handlers and <see cref="BufferedReadModelBase.Flush"/>
/// both run on the model's queue thread. Obtain one through
/// <see cref="BufferedReadModelBase.CreateBuffer{TKey,TModel}"/>, which is what lets the model know
/// when it holds something to flush.</para>
/// <para>An <see cref="Upsert"/> after a <see cref="Delete"/> of the same key is an upsert, and a
/// <see cref="Delete"/> after an <see cref="Upsert"/> is a delete: a key is in at most one of the two
/// sets, so a flush that applies <see cref="Deletes"/> and then <see cref="Upserts"/> in either order
/// lands at the last state.</para>
/// </remarks>
public sealed class WriteBuffer<TKey, TModel> : IWriteBuffer where TKey : notnull {
	private readonly Dictionary<TKey, TModel> _upserts;
	private readonly HashSet<TKey> _deletes;

	internal WriteBuffer(IEqualityComparer<TKey>? comparer) {
		_upserts = new Dictionary<TKey, TModel>(comparer);
		_deletes = new HashSet<TKey>(comparer);
	}

	/// <summary>The rows to write, by key, in their final state.</summary>
	/// <remarks>A copy, taken at the get: a caller that retains it still holds what it read.</remarks>
	public IReadOnlyDictionary<TKey, TModel> Upserts => new Dictionary<TKey, TModel>(_upserts);

	/// <summary>The keys to remove.</summary>
	/// <remarks>A copy, taken at the get.</remarks>
	public IReadOnlySet<TKey> Deletes => new HashSet<TKey>(_deletes);

	/// <summary>True while anything is waiting to be written.</summary>
	public bool HasPending => _upserts.Count > 0 || _deletes.Count > 0;

	/// <summary>Records that <paramref name="id"/> should hold <paramref name="model"/>. Does not write.</summary>
	public void Upsert(TKey id, TModel model) {
		_deletes.Remove(id);
		_upserts[id] = model;
	}

	/// <summary>Records that <paramref name="id"/> should be removed. Does not write.</summary>
	public void Delete(TKey id) {
		_upserts.Remove(id);
		_deletes.Add(id);
	}

	/// <summary>Forgets everything pending. The model calls this after a <c>Flush</c> returns.</summary>
	public void Clear() {
		_upserts.Clear();
		_deletes.Clear();
	}
}
