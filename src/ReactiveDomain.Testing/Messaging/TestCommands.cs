using System;
using System.Threading;
using ReactiveDomain.Messaging;


// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Testing;

public class TestCommands {
    public record TestMessage : IMessage {
        public Guid MsgId { get; } = Guid.NewGuid();
    }

    public record TimeoutTestCommand : Command;
    public record Fail : Command;
    public record Throw : Command;
    public record WrapException : Command;
    public record ChainedCaller : Command;
    public record AckedCommand : Command;
    public record Command1 : Command;
    public record Command2 : Command;
    public record Command3 : Command;
    public record ParentCommand : Command;
    public record ChildCommand : ParentCommand;
    public record OrderedCommand(int SequenceNumber) : Command;
    public record RemoteHandled : Command;
    public record RemoteCancel(CancellationToken Token) : Command(Token);
    //n.b. don't register a handler for this
    public record Unhandled : Command;
    public record LongRunning : Command;
    public record TypedResponse(bool FailCommand) : Command {
        public TestResponse Succeed(int data) {
            return new TestResponse(
                this,
                data);
        }
        // ReSharper disable once MemberHidesStaticFromOuterClass
        public FailedResponse Fail(Exception ex, int data) {
            return new FailedResponse(
                this,
                ex,
                data);
        }
    }
    public record DisjunctCommand : Command;
    public record UnsubscribedCommand : Command;

    public record TestResponse : Success {
        public int Data { get; }
        public TestResponse(
            TypedResponse sourceCommand,
            int data) :
            base(sourceCommand) {
            Data = data;
        }
    }
    public record FailedResponse : Messaging.Fail {
        public int Data { get; }
        public FailedResponse(
            TypedResponse sourceCommand,
            Exception exception,
            int data) :
            base(sourceCommand, exception) {
            Data = data;
        }
    }
}