
using System;
using ReactiveDomain.Bus;
using ReactiveDomain.EventStore;
using ReactiveDomain.Messaging;
using ReactiveDomain.Tests.Helpers;
using ReactiveDomain.Tests.Subscribers.QueuedSubscriber;
using Xunit;

namespace ReactiveDomain.Tests.Logging
{

    // ReSharper disable once InconsistentNaming
    public class when_commands_fired :
        with_message_logging_enabled,
        IHandle<Message>
    {
        private readonly Guid _correlationId = Guid.NewGuid();
        private IListener _listener;

        private readonly int _maxCountedCommands = 25;
        private int _multiFireCount;
        private int _testCommandCount;

        private TestCommandSubscriber _cmdHandler;


        static when_commands_fired()
        {
            BootStrap.Load();
        }

        protected override void When()
        {
            // command must have a commandHandler
            _cmdHandler = new TestCommandSubscriber(Bus);

            _multiFireCount = 0;
            _testCommandCount = 0;

            _listener = Repo.GetListener(Logging.FullStreamName);
            _listener.EventStream.Subscribe<Message>(this);

            _listener.Start(Logging.FullStreamName);

            // create and fire a set of commands
            for (int i = 0; i < _maxCountedCommands; i++)
            {
                // this is just an example command - choice to fire this one was random
                var cmd = new InformUserCmd("title",
                                        $"message{i}",
                                        Guid.NewGuid(),
                                        null);
                Bus.Fire(cmd,
                    $"exception message{i}",
                    TimeSpan.FromSeconds(2));

                Bus.Publish(
                    new CountedEvent(i,
                        _correlationId,
                        Guid.NewGuid()));

            }
            var tstCmd = new TestCommands.TestCommand3(
                        Guid.NewGuid(),
                        null);

            Bus.Fire(tstCmd,
                "Test Command exception message",
                TimeSpan.FromSeconds(1));

        }


        [Fact(Skip="pending deletion of log stream")]
        public void can_verify_commands_logged()
        {
            TestQueue.WaitFor<TestCommands.TestCommand3>(TimeSpan.FromSeconds(5));
            // Wait  for last command to be queued

            //    // Wait  for last event to be queued
            Assert.IsOrBecomesTrue(() => _multiFireCount == _maxCountedCommands, 9000);
            Assert.True(_multiFireCount == _maxCountedCommands, $"Command count {_multiFireCount} doesn't match expected index {_maxCountedCommands}");
            Assert.IsOrBecomesTrue(() => _testCommandCount == 1, 1000);

            Assert.True(_testCommandCount == 1, $"Last event count {_testCommandCount} doesn't match expected value {1}");

        }

        public void Handle(Message msg)
        {
            if (msg is InformUserCmd) _multiFireCount++;
            if (msg is TestCommands.TestCommand3) _testCommandCount++;
        }
    }
}

