namespace ReactiveDomain.Foundation;

/// <summary>
/// The live subscription dropped and could not be resumed from where it left off.
/// </summary>
public sealed class SubscriptionDroppedException : Exception {
	/// <summary>The stream the subscription was on.</summary>
	public string StreamName { get; }

	/// <summary>Why the store dropped it.</summary>
	public SubscriptionDropReason Reason { get; }

	/// <summary>
	/// The subscription to <paramref name="streamName"/> ended with <paramref name="reason"/> and
	/// could not be resumed.
	/// </summary>
	public SubscriptionDroppedException(
		string streamName,
		SubscriptionDropReason reason,
		Exception? inner = null)
		: base(
			$"The subscription to '{streamName}' dropped ({reason}) and could not be resumed.",
			inner) {
		StreamName = streamName;
		Reason = reason;
	}
}
