using System;
using System.Security.Claims;

namespace ReactiveDomain
{
    public class CommandEnvelope
    {
        public CommandEnvelope()
            : this(Guid.Empty, Guid.Empty, null, null, Metadata.None, null)
        {
        }

        private CommandEnvelope(Guid commandId, Guid correlationId, Guid? sourceId, object command, Metadata metadata, ClaimsPrincipal principal)
        {
            CommandId = commandId;
            CorrelationId = correlationId;
            SourceId = sourceId;
            Command = command;
            Metadata = metadata;
            Principal = principal;
        }

        public CommandEnvelope SetCommand(object value)
        {
            return new CommandEnvelope(CommandId, CorrelationId, SourceId, value, Metadata, Principal);
        }
        
        public CommandEnvelope SetCommandId(Guid value)
        {
            return new CommandEnvelope(value, CorrelationId, SourceId, Command, Metadata, Principal);
        }

        public CommandEnvelope SetCorrelationId(Guid value)
        {
            return new CommandEnvelope(CommandId, value, SourceId, Command, Metadata, Principal);
        }

        public CommandEnvelope SetSourceId(Guid? value)
        {
            return new CommandEnvelope(CommandId, CorrelationId, value, Command, Metadata, Principal);
        }

        public CommandEnvelope SetMetadata(Metadata value)
        {
            return new CommandEnvelope(CommandId, CorrelationId, SourceId, Command, value, Principal);
        }

        public CommandEnvelope SetPrincipal(ClaimsPrincipal value)
        {
            return new CommandEnvelope(CommandId, CorrelationId, SourceId, Command, Metadata, value);
        }

        public object Command { get; }
        public Guid CommandId { get; }
        public Guid CorrelationId { get; }
        public Guid? SourceId { get; }
        public Metadata Metadata { get; }
        public ClaimsPrincipal Principal { get; }

        public CommandEnvelope<TCommand> TypedAs<TCommand>()
        {
            return new CommandEnvelope<TCommand>(this);
        }
    }
}
