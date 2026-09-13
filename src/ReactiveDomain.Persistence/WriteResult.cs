namespace ReactiveDomain;

/// <summary>Where an append landed: the stream's new version and, when the store reports one, its <c>$all</c> position.</summary>
public struct WriteResult {
	/// <summary>The next expected version for the stream.</summary>
	public readonly long NextExpectedVersion;

	/// <summary>The <c>$all</c> position the store reported for the write, or null when it reports none.</summary>
	/// <remarks>This is never derived from the version: a null here means the store did not say, not that the write is at the start of the log.</remarks>
	public readonly Position? LogPosition;

	/// <summary>
	/// Constructs a new <see cref="WriteResult" />.
	/// </summary>
	/// <param name="nextExpectedVersion">The next expected version for the stream.</param>
	/// <param name="logPosition">The <c>$all</c> position the store reported for the write, if any.</param>
	public WriteResult(long nextExpectedVersion, Position? logPosition = null) {
		NextExpectedVersion = nextExpectedVersion;
		LogPosition = logPosition;
	}
}
