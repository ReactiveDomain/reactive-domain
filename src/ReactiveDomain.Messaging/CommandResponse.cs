using System;
using System.Threading;
using ReactiveDomain.Messaging.Messages;

namespace ReactiveDomain.Messaging
{
    public abstract class CommandResponse : Message, ICorrelatedMessage
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public Command SourceCommand { get; }
        public Type CommandType => SourceCommand.GetType();
        public Guid CommandId => SourceCommand.MsgId;
        public Guid? SourceId => SourceCommand.MsgId;
        public Guid CorrelationId => SourceCommand.CorrelationId;

        protected CommandResponse(Command sourceCommand)
        {
            SourceCommand = sourceCommand;
        }
    }

    public class Success : CommandResponse
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId => TypeId;
        public Success(Command sourceCommand) : base(sourceCommand) {}
    }
    public class Fail : CommandResponse
    {
        private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
        public override int MsgTypeId => TypeId;
        public Exception Exception { get; }
        public Fail(Command sourceCommand, Exception exception) : base(sourceCommand) 
        {
            Exception = exception;
        }
    }
}
