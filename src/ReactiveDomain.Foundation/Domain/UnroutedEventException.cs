namespace ReactiveDomain;

/// <summary>
/// An event was routed (or raised) with no handler registered for its type.
/// </summary>
public sealed class UnroutedEventException : InvalidOperationException {
	/// <summary>The event type that had no route.</summary>
	public Type EventType { get; }

	/// <summary>No route is registered for <paramref name="eventType"/>.</summary>
	public UnroutedEventException(Type eventType)
		: base(
			$"No route is registered for '{eventType.FullName}'. Register a handler, or " +
			"RegisterPassThrough if it is persisted for downstream readers and never folded.") {
		EventType = eventType;
	}
}
