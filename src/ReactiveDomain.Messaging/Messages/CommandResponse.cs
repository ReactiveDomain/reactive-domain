using System;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Messaging;

public abstract record CommandResponse(ICommand SourceCommand) : Message, ICorrelatedMessage, ICommandResponse {
    public Guid CorrelationId { get; set; } = SourceCommand.CorrelationId;
    public Guid CausationId { get; set; } = SourceCommand.MsgId;

    public Type CommandType => SourceCommand.GetType();
    public Guid CommandId => SourceCommand.MsgId;
}

public record Success(ICommand SourceCommand) : CommandResponse(SourceCommand);

public record Fail(ICommand SourceCommand, Exception Exception) : CommandResponse(SourceCommand);

public record Canceled(ICommand SourceCommand) : Fail(SourceCommand, new CommandCanceledException(SourceCommand));