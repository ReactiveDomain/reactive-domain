using System;
using System.Threading;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Tests.Helpers
{
    public class TestCommands
    {
        public class TestCommand : Command
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public TestCommand(Guid correlationId, Guid? sourceId) : base(correlationId, sourceId) { }
        }
        public class TestCommand2 : Command
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public TestCommand2(Guid correlationId, Guid? sourceId) : base(correlationId, sourceId) { }
        }
        public class TestCommand3 : Command
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public TestCommand3(Guid correlationId, Guid? sourceId) : base(correlationId, sourceId) { }
        }
        public class TypedTestCommand : Command
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public TypedTestCommand(Guid correlationId, Guid? sourceId) : base(correlationId, sourceId) { }

            public TestCommandResponse Succeed(int data)
            {
                return new TestCommandResponse(
                            this,
                            data);
            }
            public TestFailedCommandResponse Fail(Exception ex, int data)
            {
                return new TestFailedCommandResponse(
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

        public class TestCommandResponse : Success
        {
            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public int Data { get; }

            public TestCommandResponse(
                TypedTestCommand sourceCommand,
                int data) :
                    base(sourceCommand)
            {
                Data = data;
            }
        }
        public class TestFailedCommandResponse : Fail
        {

            private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);
            public override int MsgTypeId => TypeId;
            public int Data { get; }
            public TestFailedCommandResponse(
               TypedTestCommand sourceCommand,
               Exception exception,
               int data) :
                    base(sourceCommand, exception)
            {
                Data = data;
            }
        }
    }
}
