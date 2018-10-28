using System;
using Newtonsoft.Json;
using ReactiveDomain.Messaging;


// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Testing {
    public class TestCommands {
        public class TestMessage : Message {
            public TestMessage() { }
        }

        public class TimeoutTestCommand : Command {
            public TimeoutTestCommand(CorrelatedMessage source) : base(source) { }
        }
        public class Fail : Command {
            public Fail(CorrelatedMessage source) : base(source) { }
        }
        public class Throw : Command {
            public Throw(CorrelatedMessage source) : base(source) { }
        }
        public class WrapException : Command {
            public WrapException(CorrelatedMessage source) : base(source) { }
        }
        public class ChainedCaller : Command {
            public ChainedCaller(CorrelatedMessage source) : base(source) { }
        }
        public class AckedCommand : Command {
            public AckedCommand(CorrelatedMessage source) : base(source) { }
        }
        public class Command1 : Command {
            public Command1(CorrelatedMessage source) : base(source) { }
        }
        public class Command2 : Command {
            public Command2(CorrelatedMessage source) : base(source) { }
        }
        public class Command3 : Command {
            public Command3(CorrelatedMessage source) : base(source) { }
        }
        public class OrderedCommand : Command {
            public readonly int SequenceNumber;
            public OrderedCommand(int sequenceNumber, CorrelatedMessage source) : base(source) {
                SequenceNumber = sequenceNumber;
            }
        }
        public class RemoteHandled : Command {
            public RemoteHandled(CorrelatedMessage source) : base(source) { }
        }
        //n.b. don't register a handler for this
        public class Unhandled : Command {
            public Unhandled(CorrelatedMessage source) : base(source) { }
        }
        public class LongRunning : Command {
            public LongRunning(CorrelatedMessage source) : base(source) { }
        }
        public class TypedResponse : Command {
            public readonly bool FailCommand;
            public TypedResponse(
                bool failCommand,
                CorrelatedMessage source) : base(source) {
                FailCommand = failCommand;
            }
            [JsonConstructor]
            public TypedResponse(
                bool failCommand,
                CorrelationId correlationId,
                SourceId sourceId) : base(correlationId, sourceId) {
                FailCommand = failCommand;
            }
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
        public class DisjunctCommand : Command {
            public DisjunctCommand(CorrelatedMessage source) : base(source) { }
        }
        public class UnsubscribedCommand : Command {
            public UnsubscribedCommand(CorrelatedMessage source) : base(source) { }
        }

        public class TestResponse : Success {
            public int Data { get; }

            public TestResponse(
                TypedResponse sourceCommand,
                int data) :
                    base(sourceCommand) {
                Data = data;
            }
        }
        public class FailedResponse : Messaging.Fail {
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
}
