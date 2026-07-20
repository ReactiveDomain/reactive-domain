namespace ReactiveDomain;

/// <summary>
/// Additive extension of <see cref="IStreamStoreConnection"/> for gRPC-backed capabilities that grow
/// over time (server-side <c>$all</c> filtering now; snapshots and similar later).
/// <see cref="IStreamStoreConnection"/> remains the stable V1 contract — implementations must not
/// change V1 <c>SubscribeToAll</c> / <c>SubscribeToAllFrom</c> defaults when adding members here.
/// </summary>
public interface IStreamStoreConnectionGrpc : IStreamStoreConnection {
	/// <summary>
	/// Catch-up <c>$all</c> subscription with a server-side filter.
	/// <paramref name="checkpointReached"/> surfaces the server's periodic position even across spans
	/// where nothing matches, so a caller that persists positions can resume without re-scanning the
	/// filtered gap. A null <paramref name="filter"/> means no server-side filter (raw <c>$all</c>,
	/// system events and link records included). Use
	/// <see cref="StreamStoreEventFilter.ExcludeSystemEvents"/> for the domain-only view.
	/// </summary>
	IDisposable SubscribeToAllFiltered(
		Position from,
		Action<RecordedEvent> eventAppeared,
		StreamStoreEventFilter? filter,
		Action? liveProcessingStarted = null,
		Action<Position>? checkpointReached = null,
		Action<SubscriptionDropReason, Exception?>? subscriptionDropped = null,
		bool resolveLinkTos = false);
}
