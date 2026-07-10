namespace ReactiveDomain;

public struct WriteResult {
	/// <summary>The next expected version for the stream.</summary>
	public readonly long NextExpectedVersion;
	/// <summary>
	/// Constructs a new <see cref="WriteResult" />.
	/// </summary>
	/// <param name="nextExpectedVersion">The next expected version for the stream.</param>
	public WriteResult(long nextExpectedVersion) {
		NextExpectedVersion = nextExpectedVersion;
	}
}
