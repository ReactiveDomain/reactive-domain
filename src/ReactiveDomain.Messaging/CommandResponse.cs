using System;
using System.Threading;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Messaging.Messages;

namespace ReactiveDomain.Messaging
{
    public abstract class CommandResponse : CorrelatedMessage
    {
        public Command SourceCommand { get; }
        public Type CommandType => SourceCommand.GetType();
        public Guid CommandId => SourceCommand.MsgId;
        

        protected CommandResponse(Command sourceCommand):base(sourceCommand.CorrelationId, new SourceId(sourceCommand))   
        {
            SourceCommand = sourceCommand;
        }
    }

    public class Success : CommandResponse
    {
        public Success(Command sourceCommand) : base(sourceCommand) {}
    }

    public class Fail : CommandResponse
    {
        public Exception Exception { get; }
        public Fail(Command sourceCommand, Exception exception) : base(sourceCommand) 
        {
            Exception = exception;
        }
    }

    public class Canceled : Fail
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId => TypeId;
        public Canceled(Command sourceCommand) : base(sourceCommand, new CommandCanceledException(sourceCommand)) { }
    }
}
