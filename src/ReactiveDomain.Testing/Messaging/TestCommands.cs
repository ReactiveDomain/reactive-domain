using System;
using System.Threading;
using Newtonsoft.Json;
using ReactiveDomain.Messaging.Messages;

namespace ReactiveDomain.Messaging.Testing
{
    public class TestCommands
    {
        public class TimeoutTestCommand : Command
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public TimeoutTestCommand(CorrelatedMessage source) : base(source) { }
        }
        public class Fail : Command
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public Fail(CorrelatedMessage source) : base(source) { }
        }
        public class Throw : Command
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public Throw(CorrelatedMessage source) : base(source) { }
        }
        public class WrapException : Command
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public WrapException(CorrelatedMessage source) : base(source) { }
        }
        public class ChainedCaller : Command
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public ChainedCaller(CorrelatedMessage source) : base(source) { }
        }
        public class Command1 : Command
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public Command1(CorrelatedMessage source) : base(source) { }
        }
        public class Command2 : Command
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public Command2(CorrelatedMessage source) : base(source) { }
        }
        public class Command3 : Command
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public Command3(CorrelatedMessage source) : base(source) { }
        }
        public class RemoteHandled : Command
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public RemoteHandled(CorrelatedMessage source) : base(source) { }
        }
        //n.b. don't register a handler for this
        public class Unhandled : Command
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public Unhandled(CorrelatedMessage source) : base(source) { }
        }
        public class LongRunning : Command
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public LongRunning(CorrelatedMessage source) : base(source) { }
        }
        public class TypedResponse : Command
        {
            public readonly bool FailCommand;
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public TypedResponse(
                bool failCommand,
                CorrelatedMessage source) : base( source) {
                FailCommand = failCommand;
            }
            [JsonConstructor]
            public TypedResponse(
                bool failCommand,
                CorrelationId correlationId,
                SourceId sourceId) : base( correlationId,sourceId) {
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
            public DisjunctCommand(CorrelatedMessage source) : base(source) { }
        }
        public class UnsubscribedCommand : Command
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public UnsubscribedCommand(CorrelatedMessage source) : base(source) { }
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
