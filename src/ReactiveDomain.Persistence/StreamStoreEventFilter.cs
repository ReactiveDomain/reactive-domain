namespace ReactiveDomain;

/// <summary>
/// Backend-neutral description of a server-side <c>$all</c> filter. Each backend maps it to its own
/// mechanism (e.g. KurrentDB <c>IEventFilter</c>, in-memory predicate) so the same filter means the
/// same thing across implementations.
/// </summary>
public sealed class StreamStoreEventFilter {
	/// <summary>Filter discriminator for backend mapping.</summary>
	public enum FilterKind {
		/// <summary>Exclude system / link event types (types starting with <c>$</c>).</summary>
		ExcludeSystemEvents,
		/// <summary>Match event types by one or more prefixes.</summary>
		EventTypePrefix,
		/// <summary>Match event types by a regular expression.</summary>
		EventTypeRegex,
		/// <summary>Match stream ids by one or more prefixes.</summary>
		StreamPrefix,
		/// <summary>Match stream ids by a regular expression.</summary>
		StreamRegex
	}

	/// <summary>Which filter form this instance represents.</summary>
	public FilterKind Kind { get; }

	/// <summary>Prefix list for prefix-based kinds; empty otherwise.</summary>
	public IReadOnlyList<string> Prefixes { get; }

	/// <summary>Regex for regex-based kinds; null otherwise.</summary>
	public string? Regex { get; }

	private StreamStoreEventFilter(FilterKind kind, IReadOnlyList<string> prefixes, string? regex) {
		Kind = kind;
		Prefixes = prefixes;
		Regex = regex;
	}

	/// <summary>
	/// Every event whose type does not start with <c>$</c> — domain events only, no link records
	/// (<c>$&gt;</c>) and no system events.
	/// </summary>
	public static StreamStoreEventFilter ExcludeSystemEvents() =>
		new(FilterKind.ExcludeSystemEvents, [], null);

	/// <summary>Match events whose type starts with any of <paramref name="prefixes"/>.</summary>
	public static StreamStoreEventFilter EventTypePrefix(params string[] prefixes) =>
		new(FilterKind.EventTypePrefix, prefixes, null);

	/// <summary>Match events whose type matches <paramref name="regex"/>.</summary>
	public static StreamStoreEventFilter EventTypeRegex(string regex) =>
		new(FilterKind.EventTypeRegex, [], regex);

	/// <summary>Match events whose stream id starts with any of <paramref name="prefixes"/>.</summary>
	public static StreamStoreEventFilter StreamPrefix(params string[] prefixes) =>
		new(FilterKind.StreamPrefix, prefixes, null);

	/// <summary>Match events whose stream id matches <paramref name="regex"/>.</summary>
	public static StreamStoreEventFilter StreamRegex(string regex) =>
		new(FilterKind.StreamRegex, [], regex);
}
