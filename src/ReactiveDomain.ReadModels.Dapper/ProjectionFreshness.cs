using ReactiveDomain.Foundation;

namespace ReactiveDomain.ReadModels;

/// <summary>
/// Whether a projection's saved checkpoints have reached the end of each category it reads, so work
/// that trusts its rows can tell a current read model from one still catching up.
/// </summary>
/// <remarks>
/// <para>Compares the checkpoints saved in the database with the streams themselves, so it needs no handle
/// on the projection instance: any instance can ask about any projection.</para>
/// <para>Gate work that must not miss anything committed before some cut — a scheduled job completing a
/// window, say — on this: the rows always trail the streams, and work that completes against rows still
/// catching up has read a partial history as if it were the whole one.</para>
/// </remarks>
public sealed class ProjectionFreshness {
	private readonly IConfiguredConnection _connection;
	private readonly IProjectionDb _db;

	/// <summary>Compares <paramref name="db"/>'s checkpoints against the streams on <paramref name="connection"/>.</summary>
	public ProjectionFreshness(IConfiguredConnection connection, IProjectionDb db) {
		_connection = connection;
		_db = db;
	}

	/// <summary>True when <paramref name="projection"/>'s saved checkpoints have reached the end of every category given.</summary>
	/// <remarks>The ends are read after the call began, so a true answer covers everything committed before any earlier moment.</remarks>
	public bool CaughtUp(string projection, params Type[] categories) {
		var saved = _db.Checkpoints.Load(projection).ToDictionary(c => c.StreamName, c => c.Version);
		foreach (var category in categories) {
			var stream = _connection.StreamNamer.GenerateForCategory(category);
			StreamEventsSlice? slice;
			try {
				slice = _connection.Connection.ReadStreamBackward(stream, -1, 1);
			} catch (StreamNotFoundException) {
				continue;
			}
			if (slice is null or StreamNotFoundSlice || slice.Events.Length == 0)
				continue;
			if (!saved.TryGetValue(stream, out var version) || version is null || version < slice.LastEventNumber)
				return false;
		}
		return true;
	}
}
