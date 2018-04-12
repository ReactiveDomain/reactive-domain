using ReactiveDomain.Foundation.EventStore;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Messaging.Testing;
using ReactiveDomain.Testing;
using System;
using ReactiveDomain.Messaging.Messages;
using Xunit;
using Xunit.Sdk;

namespace ReactiveDomain.Foundation.Tests.Logging
{

    // ReSharper disable once InconsistentNaming
    [Collection(nameof(EmbeddedStreamStoreConnectionCollection))]
    public class when_logging_disabled_and_commands_are_fired :
        with_message_logging_disabled,
        IHandle<Message>
    {
        static when_logging_disabled_and_commands_are_fired()
        {
            BootStrap.Load();
        }

        
            
        
        private readonly Guid _correlationId = Guid.NewGuid();
        private IListener _listener;

        private readonly int _maxCountedCommands = 25;
        private int _multiFireCount;
        private int _testCommandCount;

        private TestCommandSubscriber _cmdHandler;

        public when_logging_disabled_and_commands_are_fired(StreamStoreConnectionFixture fixture):base(fixture.Connection)
        {
            // command must have a commandHandler
            _cmdHandler = new TestCommandSubscriber(Bus);

            _multiFireCount = 0;
            _testCommandCount = 0;

            _listener = new SynchronizableStreamListener(Logging.FullStreamName, Connection, StreamNameBuilder);
            _listener.EventStream.Subscribe<Message>(this);

            _listener.Start(Logging.FullStreamName);
            CorrelatedMessage source = CorrelatedMessage.NewRoot();
            // create and fire a set of commands
            for (int i = 0; i < _maxCountedCommands; i++)
            {
                // this is just an example command - choice to fire this one was random
                var cmd = new TestCommands.Command2(source);
                Bus.Fire(cmd,
                    $"exception message{i}",
                    TimeSpan.FromSeconds(2));
                source = cmd;

            }
            var tstCmd = new TestCommands.Command3(source);

            Bus.Fire(tstCmd,
                "Test Command exception message",
                TimeSpan.FromSeconds(1));

        }

        public void commands_are_not_logged()
        {
            Assert.IsOrBecomesTrue(()=> _cmdHandler.TestCommand3Handled >0);
            // Wait  for last command to be queued

            //    // Wait  for last event to be queued
            Assert.Throws<TrueException>(() => Assert.IsOrBecomesTrue(
                () => _multiFireCount > 0, 
                1000,
                $"Commands logged to ES when logging should be disabled - {_multiFireCount}"));

            Assert.Throws<TrueException>(() => Assert.IsOrBecomesTrue(
                () => _testCommandCount == 1,
                1000,
                $"Last command logged to ES when logging should be disabled"));

            Assert.False(_multiFireCount == _maxCountedCommands, $"Command count {_multiFireCount} doesn't match expected index {_maxCountedCommands}");
            Assert.False(_testCommandCount == 1, $"Last event count {_testCommandCount} doesn't match expected value {1}");

        }

        public void Handle(Message msg)
        {
            if (msg is TestCommands.Command2) _multiFireCount++;
            if (msg is TestCommands.Command3) _testCommandCount++;
        }

       
     
    }
}
