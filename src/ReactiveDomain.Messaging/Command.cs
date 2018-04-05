using System;
using System.Threading;
using ReactiveDomain.Messaging.Messages;

namespace ReactiveDomain.Messaging
{
    /// <summary>
    /// A correlated command that is optionally cancellable using a CancellationToken. 
    /// </summary>
    /// <inheritdoc cref="Message"/>
    public class Command : Message, ICorrelatedMessage
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId => TypeId;

        /// <inheritdoc cref="ICorrelatedMessage"/>
        public Guid? SourceId { get; }

        /// <inheritdoc cref="ICorrelatedMessage"/>
        public Guid CorrelationId { get; }

        public bool TimeoutTcpWait = true;

        /// <summary>
        /// The CancellationToken for this command
        /// </summary>
        public readonly CancellationToken? CancellationToken;

        /// <summary>
        /// Has this command been canceled?
        /// </summary>
        public bool IsCanceled => CancellationToken?.IsCancellationRequested ?? false;

        /// <summary>
        /// Does this command allow cancellation?
        /// </summary>
        public bool IsCancelable => CancellationToken != null;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="correlationId">The unique ID for this message chain.</param>
        /// <param name="sourceId">The unique ID of the antecedent message.</param>
        /// <param name="cancellationToken">An optional token for canceling the command. Defaults to null</param>
        public Command(
            Guid correlationId,
            Guid? sourceId,
            CancellationToken? cancellationToken = null)
        {
            CorrelationId = correlationId;
            SourceId = sourceId;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Create a CommandResponse indicating that this command has succeeded.
        /// </summary>
        public CommandResponse Succeed()
        {
            return new Success(this);
        }

        /// <summary>
        /// Create a CommandResponse indicating that this command has failed.
        /// </summary>
        public CommandResponse Fail(Exception ex = null)
        {
            return new Fail(this, ex);
        }

        /// <summary>
        /// Create a CommandResponse indicating that this command has been canceled.
        /// </summary>
        public CommandResponse Canceled()
        {
            return new Canceled(this);
        }
    }

    /// <summary>
    /// Indicates receipt of a command message
    /// Does not indicate success or failure of command processing
    /// </summary>
    public class AckCommand : Message, ICorrelatedMessage
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId => TypeId;
        public Command SourceCommand { get; }
        public Guid? SourceId => SourceCommand.MsgId;
        public Guid CorrelationId => SourceCommand.CorrelationId;
        public Guid CommandId => SourceCommand.MsgId;
        public Type CommandType => SourceCommand.GetType();

        public AckCommand(Command sourceCommand)
        {
            SourceCommand = sourceCommand;
        }
    }
}
