using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Messaging;

public abstract record CommandResponse(ICommand SourceCommand) : Message, ICorrelatedMessage, ICommandResponse {
	public Guid CorrelationId { get; set; } = SourceCommand.CorrelationId;
	public Guid CausationId { get; set; } = SourceCommand.MsgId;

	public Type CommandType => SourceCommand.GetType();
	public Guid CommandId => SourceCommand.MsgId;
}

/// <summary>The command was handled. Carries where the handling's writes landed, when the handler reports them.</summary>
/// <remarks>
/// <para><see cref="WritePositions"/> is the read-your-writes token: a read model whose checkpoints
/// compare <see cref="CheckpointOrder.Equal"/> or <see cref="CheckpointOrder.After"/> to it through
/// <see cref="StreamCheckpoint.Compare"/> has been delivered everything the handling wrote. One entry
/// per stream written; a multi-write handler reports each. Empty means the handler reported nothing,
/// not that nothing was written.</para>
/// <para>Compared component-wise. The list has no total order and
/// <see cref="CheckpointOrder.Concurrent"/> is a legitimate answer.</para>
/// </remarks>
public record Success(ICommand SourceCommand) : CommandResponse(SourceCommand) {
	/// <summary>The stream positions the handling wrote, as its <c>Save</c> calls reported them.</summary>
	public IReadOnlyList<StreamCheckpoint> WritePositions { get; init; } = [];
}

public record Fail(ICommand SourceCommand, Exception? Exception) : CommandResponse(SourceCommand);

public record Canceled(ICommand SourceCommand) : Fail(SourceCommand, new CommandCanceledException(SourceCommand));
