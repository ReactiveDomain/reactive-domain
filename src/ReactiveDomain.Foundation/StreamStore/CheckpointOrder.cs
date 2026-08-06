// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation;

/// <summary>
/// How one set of checkpoints stands to another: the four answers a partial order can give.
/// </summary>
/// <remarks>
/// Checkpoints over several streams are not totally ordered, so <see cref="Concurrent"/> is an
/// answer and not a failure to produce one. Collapsing it into <see cref="Before"/> or
/// <see cref="After"/> — by comparing a single projected position, say — reports an order that does
/// not hold.
/// </remarks>
public enum CheckpointOrder {
	/// <summary>Both cover exactly the same events on every stream.</summary>
	Equal,

	/// <summary>Covered by the other on every stream, and short of it on at least one.</summary>
	Before,

	/// <summary>Covers the other on every stream, and goes beyond it on at least one.</summary>
	After,

	/// <summary>Ahead on one stream and behind on another, so neither covers the other.</summary>
	Concurrent
}
