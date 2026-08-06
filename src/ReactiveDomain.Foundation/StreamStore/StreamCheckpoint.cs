using ReactiveDomain.Util;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation;

/// <summary>
/// How far a read model has applied one stream: the stream's own version, and where that event sits
/// in the store's global <c>$all</c> log.
/// </summary>
/// <remarks>
/// <para>The two are different clocks and answer different questions. <see cref="Version"/> is dense
/// and per-stream, so it is what a subscription resumes from and the only one that can express "the
/// next event". <see cref="Position"/> is a store-wide log position that supports comparison and
/// nothing else — no successor, no distance — but it is comparable <i>across</i> streams, which
/// versions are not.</para>
/// <para><see cref="Position"/> is null when the store does not report one, and is meaningful only
/// within the store that issued it. Positions from two stores have no defined ordering.</para>
/// </remarks>
public sealed record StreamCheckpoint {
	/// <summary>The stream this checkpoint is for.</summary>
	public string StreamName { get; }

	/// <summary>The version of the last event applied from that stream.</summary>
	public long Version { get; }

	/// <summary>
	/// The <c>$all</c> position of the last event applied from that stream, when the store reports one.
	/// </summary>
	public Position? Position { get; }

	/// <summary>Records how far a stream has been applied.</summary>
	/// <param name="streamName">The stream this checkpoint is for.</param>
	/// <param name="version">The version of the last event applied from that stream.</param>
	/// <param name="position">That event's <c>$all</c> position, when the store reports one.</param>
	public StreamCheckpoint(string streamName, long version, Position? position = null) {
		Ensure.NotNullOrEmpty(streamName, nameof(streamName));
		StreamName = streamName;
		Version = version;
		Position = position;
	}
}
