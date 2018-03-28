using System;
using System.Threading;

namespace ReactiveDomain.Messaging.Testing
{
    public class TestCommands
    {
        public class TimeoutTestCommand : Command
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public TimeoutTestCommand(Guid correlationId, Guid? sourceId) : base(correlationId, sourceId) { }
        }
        public class Fail : Command
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public Fail(Guid correlationId, Guid? sourceId) : base(correlationId, sourceId) { }
        }
        public class Throw : Command
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public Throw(Guid correlationId, Guid? sourceId) : base(correlationId, sourceId) { }
        }
        public class WrapException : Command
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public WrapException(Guid correlationId, Guid? sourceId) : base(correlationId, sourceId) { }
        }
        public class ChainedCaller : Command
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public ChainedCaller(Guid correlationId, Guid? sourceId) : base(correlationId, sourceId) { }
        }
        public class Command1 : Command
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public Command1(Guid correlationId, Guid? sourceId) : base(correlationId, sourceId) { }
        }
        public class Command2 : Command
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public Command2(Guid correlationId, Guid? sourceId) : base(correlationId, sourceId) { }
        }
        public class Command3 : Command
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public Command3(Guid correlationId, Guid? sourceId) : base(correlationId, sourceId) { }
        }
        public class RemoteHandled : Command
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public RemoteHandled(Guid correlationId, Guid? sourceId) : base(correlationId, sourceId) { }
        }
        //n.b. don't register a handler for this
        public class Unhandled : Command
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public Unhandled(Guid correlationId, Guid? sourceId) : base(correlationId, sourceId) { }
        }
        public class LongRunning : Command
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public LongRunning(Guid correlationId, Guid? sourceId) : base(correlationId, sourceId) { }
        }
        public class TypedResponse : Command
        {
            public readonly bool FailCommand;
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public TypedResponse(
                bool failCommand,
                Guid correlationId, 
                Guid? sourceId) : base(correlationId, sourceId) {
                FailCommand = failCommand;
            }

            public TestResponse Succeed(int data)
            {
                return new TestResponse(
                            this,
                            data);
            }
            public FailedResponse Fail(Exception ex, int data)
            {
                return new FailedResponse(
                            this,
                            ex,
                            data);
            }
        }
        public class DisjunctCommand : Command
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public DisjunctCommand(Guid correlationId, Guid? sourceId) : base(correlationId, sourceId) { }
        }
        public class UnsubscribedCommand : Command
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public UnsubscribedCommand(Guid correlationId, Guid? sourceId) : base(correlationId, sourceId) { }
        }

        public class TestResponse : Success
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public int Data { get; }

            public TestResponse(
                TypedResponse sourceCommand,
                int data) :
                    base(sourceCommand)
            {
                Data = data;
            }
        }
        public class FailedResponse : Messaging.Fail
        {

            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public int Data { get; }
            public FailedResponse(
               TypedResponse sourceCommand,
               Exception exception,
               int data) :
                    base(sourceCommand, exception)
            {
                Data = data;
            }
        }
    }
}
