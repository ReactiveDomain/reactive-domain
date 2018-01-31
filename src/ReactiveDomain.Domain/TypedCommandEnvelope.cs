using System;
using System.Security.Claims;

namespace ReactiveDomain
{
    public class CommandEnvelope<TCommand>
    {
        internal CommandEnvelope(CommandEnvelope envelope)
        {
            if (envelope == null)
            {
                throw new ArgumentNullException(nameof(envelope));
            }

            Command = (TCommand)envelope.Command;
            CommandId = envelope.CommandId;
            CorrelationId = envelope.CorrelationId;
            SourceId = envelope.SourceId;
            Metadata = envelope.Metadata;
            Principal = envelope.Principal;
        }

        private CommandEnvelope(Guid commandId, Guid correlationId, Guid? sourceId, TCommand command, Metadata metadata, ClaimsPrincipal principal)
        {
            CommandId = commandId;
            CorrelationId = correlationId;
            SourceId = sourceId;
            Command = command;
            Metadata = metadata;
            Principal = principal;
        }

        public CommandEnvelope<TCommand> SetCommandId(Guid value)
        {
            return new CommandEnvelope<TCommand>(value, CorrelationId, SourceId, Command, Metadata, Principal);
        }

        public CommandEnvelope<TCommand> SetCorrelationId(Guid value)
        {
            return new CommandEnvelope<TCommand>(CommandId, value, SourceId, Command, Metadata, Principal);
        }

        public CommandEnvelope<TCommand> SetSourceId(Guid? value)
        {
            return new CommandEnvelope<TCommand>(CommandId, CorrelationId, value, Command, Metadata, Principal);
        }

        public CommandEnvelope<TCommand> SetCommand(TCommand value)
        {
            return new CommandEnvelope<TCommand>(CommandId, CorrelationId, SourceId, value, Metadata, Principal);
        }

        public CommandEnvelope<TCommand> SetMetadata(Metadata value)
        {
            return new CommandEnvelope<TCommand>(CommandId, CorrelationId, SourceId, Command, value, Principal);
        }

        public CommandEnvelope<TCommand> SetPrincipal(ClaimsPrincipal value)
        {
            return new CommandEnvelope<TCommand>(CommandId, CorrelationId, SourceId, Command, Metadata, value);
        }

        public TCommand Command { get; }
        public Guid CommandId { get; }
        public Guid CorrelationId { get; }
        public Guid? SourceId { get; }
        public Metadata Metadata { get; }
        public ClaimsPrincipal Principal { get; }
    }
}