using System;
using System.Threading;
using Newtonsoft.Json;

namespace ReactiveDomain.Messaging
{
    /// <summary>
    /// A correlated command that is optionally cancellable using a CancellationToken. 
    /// </summary>
    /// <inheritdoc cref="Message"/>
    public abstract class Command : Message, ICorrelatedMessage, ICommand
    {

        public Guid CorrelationId { get; set; }
        public Guid CausationId { get; set; }


        public bool TimeoutTcpWait = true;

        /// <summary>
        /// The CancellationToken for this command
        /// </summary>
        [JsonIgnore]
        public CancellationToken? CancellationToken { get; private set; }

        /// <summary>
        /// Has this command been canceled?
        /// </summary>
        public bool IsCanceled => CancellationToken?.IsCancellationRequested ?? false;

        /// <summary>
        /// Does this command allow cancellation?
        /// </summary>
        public bool IsCancelable => CancellationToken != null;


        public virtual void RegisterOnCancellation(Action action)
        {
            if (!IsCancelable)
            {
                throw new InvalidOperationException("Command cannot be canceled");
            }
            CancellationToken?.Register(action);
        }

        public Command(CancellationToken? token = null)
        {
            CancellationToken = token;
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
    /// Indicates receipt of a command message.
    /// Does not indicate success or failure of command processing.
    /// </summary>
    /// <inheritdoc cref="Message"/>
    public class AckCommand : Message, ICorrelatedMessage
    {
        public Guid CorrelationId { get; set; }
        public Guid CausationId { get; set; }
        /// <summary>
        /// The Command whose receipt is being acknowledged.
        /// </summary>
        public ICommand SourceCommand { get; }
        /// <summary>
        /// The unique ID of the Command whose receipt is being acknowledged.
        /// </summary>
        public Guid CommandId => SourceCommand.MsgId;
        /// <summary>
        /// The Type of the Command whose receipt is being acknowledged.
        /// </summary>
        public Type CommandType => SourceCommand.GetType();

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="sourceCommand">The Command whose receipt is being acknowledged.</param>
        public AckCommand(ICommand sourceCommand)
        {

            SourceCommand = sourceCommand;
            CorrelationId = sourceCommand.CorrelationId;
            CausationId = sourceCommand.MsgId;
        }

    }
}
