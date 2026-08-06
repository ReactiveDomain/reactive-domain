using ReactiveDomain.Util;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation;

/// <summary>
/// How far a stream has been delivered to a read model: the stream's own version, and where that
/// event sits in the store's global <c>$all</c> log.
/// </summary>
/// <remarks>
/// <para>The two are different clocks and answer different questions. <see cref="Version"/> is dense
/// and per-stream, so it is what a subscription resumes from and the only one that can express "the
/// next event". <see cref="Position"/> is a store-wide log position that supports comparison and
/// nothing else — no successor, no distance — but it is comparable <i>across</i> streams, which
/// versions are not.</para>
/// <para><see cref="Position"/> is null when the store does not report one, and is meaningful only
/// within the store that issued it. Positions from two stores have no defined ordering.</para>
/// <para><b>Delivered, not applied.</b> A checkpoint is recorded when an event is handed to the
/// model's queue, which is ahead of where its handlers have run — see
/// <see cref="ReadModelBase.GetCheckpoint"/> for what that costs a snapshot.</para>
/// </remarks>
public sealed record StreamCheckpoint {
	/// <summary>The stream this checkpoint is for.</summary>
	public string StreamName { get; }

	/// <summary>
	/// The version of the last event delivered from that stream, or null when none has been: a
	/// stream that is being listened to but has produced nothing yet.
	/// </summary>
	/// <remarks>
	/// Null and 0 are not interchangeable. A version resumes a subscription <i>after</i> it, so
	/// restoring a stream whose first event was never delivered from version 0 would skip that
	/// event. Null resumes from the beginning of the stream, which is what such a checkpoint means.
	/// </remarks>
	public long? Version { get; }

	/// <summary>
	/// The <c>$all</c> position of the last event delivered from that stream, when the store reports one.
	/// </summary>
	public Position? Position { get; }

	/// <summary>Records how far a stream has been delivered.</summary>
	/// <param name="streamName">The stream this checkpoint is for.</param>
	/// <param name="version">The version of the last event delivered from that stream, or null when
	/// none has been.</param>
	/// <param name="position">That event's <c>$all</c> position, when the store reports one.</param>
	public StreamCheckpoint(string streamName, long? version, Position? position = null) {
		Ensure.NotNullOrEmpty(streamName, nameof(streamName));
		StreamName = streamName;
		Version = version;
		Position = position;
	}

	// Before every version, including 0, which is what a stream that has delivered nothing covers.
	private const long Nothing = -1;

	/// <summary>
	/// How the events covered by one set of checkpoints stand to those covered by another.
	/// </summary>
	/// <param name="first">A set of checkpoints. Null and empty both cover nothing.</param>
	/// <param name="second">The set to compare it against.</param>
	/// <remarks>
	/// <para>Compared per stream and combined, never through a single projected number. Ahead on one
	/// stream and behind on another is <see cref="CheckpointOrder.Concurrent"/>, which a scalar cannot
	/// express — see <see cref="ReadModelBase.LowestAppliedPosition"/> for what those are and are not
	/// for.</para>
	/// <para><see cref="Version"/> is the only clock this reads. Within a stream it and
	/// <see cref="Position"/> order alike, and versions are dense where positions are not, so a
	/// checkpoint without a position compares as well as one with.</para>
	/// <para>A stream in one set and not the other counts as covering nothing there, so a model that
	/// starts another stream moves strictly forward rather than becoming incomparable. That also means
	/// this answers about the checkpoints handed to it, not about the models they came from:
	/// comparing snapshots of two <i>different</i> models is arithmetic on unrelated streams, and the
	/// answer means nothing even when it is not <see cref="CheckpointOrder.Concurrent"/>.</para>
	/// </remarks>
	public static CheckpointOrder Compare(
		IEnumerable<StreamCheckpoint>? first,
		IEnumerable<StreamCheckpoint>? second) {
		var left = Covered(first);
		var right = Covered(second);
		var behind = false;
		var ahead = false;
		foreach (var stream in left.Keys.Union(right.Keys)) {
			var l = left.GetValueOrDefault(stream, Nothing);
			var r = right.GetValueOrDefault(stream, Nothing);
			if (l < r) { behind = true; } else if (l > r) { ahead = true; }
		}
		return (behind, ahead) switch {
			(false, false) => CheckpointOrder.Equal,
			(true, false) => CheckpointOrder.Before,
			(false, true) => CheckpointOrder.After,
			_ => CheckpointOrder.Concurrent
		};
	}

	/// <summary>
	/// The version each stream is covered through. Two checkpoints for one stream would be a caller's
	/// mistake; the further of them is taken rather than whichever was enumerated last.
	/// </summary>
	private static Dictionary<string, long> Covered(IEnumerable<StreamCheckpoint>? checkpoints) {
		var covered = new Dictionary<string, long>(StringComparer.Ordinal);
		if (checkpoints is null)
			return covered;
		foreach (var checkpoint in checkpoints) {
			var version = checkpoint.Version ?? Nothing;
			covered[checkpoint.StreamName] = covered.TryGetValue(checkpoint.StreamName, out var seen)
				? Math.Max(seen, version)
				: version;
		}
		return covered;
	}
}
