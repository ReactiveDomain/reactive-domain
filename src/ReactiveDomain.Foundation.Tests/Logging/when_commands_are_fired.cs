using System;
using System.Threading;
using Xunit;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;


namespace ReactiveDomain.Foundation.Tests.Logging {

    // ReSharper disable once InconsistentNaming
    [Collection(nameof(EmbeddedStreamStoreConnectionCollection))]
    public class when_commands_are_fired :
        with_message_logging_enabled,
        IHandle<Message> {

        private const int MaxCountedCommands = 25;
        private int _multiFireCount;
        private int _testCommandCount;

        public when_commands_are_fired(StreamStoreConnectionFixture fixture) : base(fixture.Connection) {

            Bus.Subscribe(new AdHocCommandHandler<TestCommands.Command2>(_ => true));
            Bus.Subscribe(new AdHocCommandHandler<TestCommands.Command3>(_ => true));
            Bus.Subscribe<Message>(this);

            _multiFireCount = 0;
            _testCommandCount = 0;


            CorrelatedMessage source = CorrelatedMessage.NewRoot();
            // create and fire a set of commands
            for (int i = 0; i < MaxCountedCommands; i++) {
                // this is just an example command - choice to fire this one was random
                var cmd = new TestCommands.Command2(source);
                Bus.Send(cmd,
                    $"exception message{i}",
                    TimeSpan.FromSeconds(2));
                source = cmd;

            }


            Bus.Send(new TestCommands.Command3(source),
                "Test Command exception message",
                TimeSpan.FromSeconds(1));

            Assert.IsOrBecomesTrue(() => _testCommandCount == 1, 1000, "Set Setup failed: Timed out waiting for last cmd");
            Assert.IsOrBecomesTrue(() => _multiFireCount == MaxCountedCommands, 9000, "Didn't get all commands");
        }


        [Fact]
        private void all_commands_are_logged() {
            var totalEvents = _multiFireCount + _testCommandCount;
            var events = Connection.ReadStreamForward(Logging.FullStreamName, StreamPosition.Start, totalEvents);
            Assert.True(events.LastEventNumber == totalEvents);
        }

        public void Handle(Message msg) {
            if (msg is TestCommands.Command2) Interlocked.Increment(ref _multiFireCount);
            if (msg is TestCommands.Command3) Interlocked.Increment(ref _testCommandCount);
        }
    }
}

