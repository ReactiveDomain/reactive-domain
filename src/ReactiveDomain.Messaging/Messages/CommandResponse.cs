using System;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Messaging
{
    public abstract class CommandResponse : Message, ICorrelatedMessage, ICommandResponse
    {
        public Guid CorrelationId { get; set; }
        public Guid CausationId { get; set; }

        public ICommand SourceCommand { get; }
        public Type CommandType => SourceCommand.GetType();
        public Guid CommandId => SourceCommand.MsgId;

        protected CommandResponse(ICommand sourceCommand)
        {
            CorrelationId = sourceCommand.CorrelationId;
            CausationId = sourceCommand.MsgId;
            SourceCommand = sourceCommand;
        }
    }

    public class Success : CommandResponse
    {
        public Success(ICommand sourceCommand) : base(sourceCommand) {}
    }

    public class Fail : CommandResponse
    {
        public Exception Exception { get; }
        public Fail(ICommand sourceCommand, Exception exception) : base(sourceCommand) 
        {
            Exception = exception;
        }
    }

    public class Canceled : Fail
    {
        public Canceled(ICommand sourceCommand) : base(sourceCommand, new CommandCanceledException(sourceCommand)) { }
    }
}
