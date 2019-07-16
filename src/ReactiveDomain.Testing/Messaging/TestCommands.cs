using System;
using System.Threading;
using Newtonsoft.Json;
using ReactiveDomain.Messaging;


// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Testing
{
    public class TestCommands
    {
        public class TestMessage : IMessage
        {
            public Guid MsgId { get; set; }
            public TestMessage() {
                MsgId = Guid.NewGuid();
            }
        }

        public class TimeoutTestCommand : Command { }
        public class Fail : Command { }
        public class Throw : Command { }
        public class WrapException : Command { }
        public class ChainedCaller : Command { }
        public class AckedCommand : Command { }
        public class Command1 : Command { }
        public class Command2 : Command { }
        public class Command3 : Command { }
        public class ParentCommand : Command { }
        public class ChildCommand : ParentCommand { }
        public class OrderedCommand : Command
        {
            public readonly int SequenceNumber;
            public OrderedCommand(int sequenceNumber)
            {
                SequenceNumber = sequenceNumber;
            }
        }
        public class RemoteHandled : Command { }
        public class RemoteCancel : Command
        {
            public RemoteCancel(CancellationToken token) : base(token) { }
        }
        //n.b. don't register a handler for this
        public class Unhandled : Command { }
        public class LongRunning : Command { }
        public class TypedResponse : Command
        {
            public readonly bool FailCommand;
            public TypedResponse(bool failCommand)
            {
                FailCommand = failCommand;
            }
            public TestResponse Succeed(int data)
            {
                return new TestResponse(
                            this,
                            data);
            }
            // ReSharper disable once MemberHidesStaticFromOuterClass
            public FailedResponse Fail(Exception ex, int data)
            {
                return new FailedResponse(
                            this,
                            ex,
                            data);
            }
        }
        public class DisjunctCommand : Command { }
        public class UnsubscribedCommand : Command { }

        public class TestResponse : Success
        {
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
