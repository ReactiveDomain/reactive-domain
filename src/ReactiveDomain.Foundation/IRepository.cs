using System.Diagnostics.CodeAnalysis;

namespace ReactiveDomain.Foundation;

public interface IRepository {
	bool TryGetById<TAggregate>(Guid id, [NotNullWhen(true)] out TAggregate? aggregate, int version = int.MaxValue) where TAggregate : class, IEventSource;
	TAggregate GetById<TAggregate>(Guid id, int version = int.MaxValue) where TAggregate : class, IEventSource;
	void Update<TAggregate>(ref TAggregate aggregate, int version = int.MaxValue) where TAggregate : class, IEventSource;
	/// <summary>Appends the aggregate's recorded events to its stream and reports where the stream now stands.</summary>
	/// <remarks>
	/// <para>The returned checkpoint is the wait target for read-your-writes: a read model whose
	/// <see cref="ReadModelBase.GetCheckpoint"/> compares <see cref="CheckpointOrder.Equal"/> or
	/// <see cref="CheckpointOrder.After"/> to it through <see cref="StreamCheckpoint.Compare"/> has
	/// been delivered everything this save wrote. Its <see cref="StreamCheckpoint.Position"/> is set
	/// only when the store reported one; it is never derived from the version.</para>
	/// <para>With nothing recorded, nothing is written and the checkpoint is the stream's version as the
	/// aggregate knows it, with no position.</para>
	/// </remarks>
	/// <param name="aggregate">The aggregate whose recorded events to persist.</param>
	/// <returns>The aggregate's stream at the version it has after the save.</returns>
	StreamCheckpoint Save(IEventSource aggregate);
	void Delete(IEventSource aggregate);
	void HardDelete(IEventSource aggregate);
}
