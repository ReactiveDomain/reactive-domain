using ReactiveDomain.Messaging;

namespace ReactiveDomain.Foundation;

/// <summary>
/// The in-band signal that every source a read model started — own streams and relays alike — has
/// handed over its history. Handlers flush catch-up caches and publish live state here.
/// </summary>
/// <remarks>
/// Enqueued on the model's own queue when <see cref="ReadModelBase.IsLive"/> completes (see
/// <see cref="ModelWentLiveArming.SignalWhenLive"/>), so the handler runs behind everything those
/// histories delivered. Handling is idempotent by contract: the arming continuation and
/// <see cref="CatchUpConnection.WaitForCatchUp"/> may each enqueue one. A test over a null
/// connection applies it directly (<see cref="ReadModelBase.DirectApply"/>) to declare liveness at
/// a chosen point.
/// </remarks>
public sealed record ModelWentLive : IMessage {
	/// <inheritdoc cref="IMessage.MsgId"/>
	public Guid MsgId { get; } = Guid.NewGuid();
}

/// <summary>Arms a read model to receive <see cref="ModelWentLive"/> when it goes live.</summary>
public static class ModelWentLiveArming {
	/// <summary>
	/// Enqueues <see cref="ModelWentLive"/> onto <paramref name="model"/>'s queue once every started
	/// source has handed over its history. Call at every composition site, after the model's last
	/// <c>Start</c> call — a model whose site forgets this never flushes its catch-up caches.
	/// </summary>
	/// <remarks>
	/// <para>Arm after the last <c>Start</c>: <see cref="ReadModelBase.IsLive"/> spans the streams
	/// started so far, so a marker armed earlier fires ahead of the histories it is meant to trail.
	/// Armed by the composition, not the model's constructor, so a test fixture over a null
	/// connection keeps deterministic control: a null read drains instantly, and a
	/// constructor-armed marker would race the fixture's own <see cref="ReadModelBase.DirectApply"/>
	/// calls.</para>
	/// <para>A continuation rather than an await because the flush must run on the model's queue
	/// thread under its reader lock; the marker gets it there. A faulted source faults
	/// <see cref="ReadModelBase.IsLive"/> and no marker is sent — the caches stay visibly unflushed
	/// rather than flushing over a model missing history.</para>
	/// </remarks>
	/// <param name="model">The read model to arm; its composition site owns this call.</param>
	public static void SignalWhenLive(this ReadModelBase model) =>
		model.IsLive.ContinueWith(
			_ => model.Handle(new ModelWentLive()),
			CancellationToken.None,
			TaskContinuationOptions.OnlyOnRanToCompletion,
			TaskScheduler.Default);
}
