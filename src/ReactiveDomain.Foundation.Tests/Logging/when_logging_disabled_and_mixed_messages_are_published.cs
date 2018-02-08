using System;
using ReactiveDomain.Foundation.Tests.EventStore;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Messaging.Testing;
using ReactiveDomain.Testing;
using Xunit;
using Xunit.Sdk;

namespace ReactiveDomain.Foundation.Tests.Logging
{

    // ReSharper disable once InconsistentNaming
    [Collection(nameof(EventStoreCollection))]
    public class when_logging_disabled_and_mixed_messages_are_published :
        with_message_logging_disabled,
        IHandle<Message>
    {
        static when_logging_disabled_and_mixed_messages_are_published()
        {
            BootStrap.Load();
        }

        public when_logging_disabled_and_mixed_messages_are_published(EmbeddedEventStoreFixture fixture):base(fixture.Connection)
        {
            
        }
        private readonly Guid _correlationId = Guid.NewGuid();
        private IListener _listener;

        private readonly int _maxCountedEvents = 5;
        private int _countedEventCount;
        private int _testDomainEventCount;

        private int _multiFireCount;
        private int _testCommandCount;

        private readonly int _maxCountedMessages = 25;

        private TestCommandSubscriber _cmdHandler;


        protected override void When()
        {

            _listener = Repo.GetListener(Logging.FullStreamName);
            _listener.EventStream.Subscribe<Message>(this);

            _listener.Start(Logging.FullStreamName);

            _countedEventCount = 0;
            _testDomainEventCount = 0;

            _cmdHandler = new TestCommandSubscriber(Bus);

            _multiFireCount = 0;
            _testCommandCount = 0;

            _listener = Repo.GetListener(Logging.FullStreamName);
            _listener.EventStream.Subscribe<Message>(this);

            _listener.Start(Logging.FullStreamName);

            // create and fire a mixed set of commands and events
            for (int i = 0; i < _maxCountedMessages; i++)
            {
                Bus.Publish(
                    new CountedEvent(i,
                        _correlationId,
                        Guid.NewGuid()));

                // this is just an example command - choice to fire this one was random
                var cmd = new TestCommands.TestCommand2(
                                        Guid.NewGuid(),
                                        null);
                Bus.Fire(cmd,
                    $"exception message{i}",
                    TimeSpan.FromSeconds(2));
            }

            for (int i = 0; i < _maxCountedEvents; i++)
            {
                Bus.Publish(new TestDomainEvent(_correlationId, Guid.NewGuid()));
            }

            var tstCmd = new TestCommands.TestCommand3(
                        Guid.NewGuid(),
                        null);

            Bus.Fire(tstCmd,
                "Test Command exception message",
                TimeSpan.FromSeconds(1));

        }



        public void mixed_messages_are_not_logged()
        {
            // all events published, commands fired
            TestQueue.WaitFor<TestCommands.TestCommand3>(TimeSpan.FromSeconds(5));

            // Wait  for last CountedEvent to be "heard" from logger/repo - times out because events not logged
            Assert.Throws<TrueException>(() => Assert.IsOrBecomesTrue(
                () => _countedEventCount > 0, 
                1000,
                $"Found {_countedEventCount} CountedEvents on log"));

            Assert.True(_countedEventCount == 0,
                         $"{_countedEventCount} CountedEvents found on Log");

            // Wait  for last command to be queued - times out because events not logged
            Assert.Throws<TrueException>(() =>Assert.IsOrBecomesTrue(
                () => _multiFireCount > 0,
                9000,
                $"Found {_multiFireCount} TestCommand2s on log - expected 0"));

            Assert.True(_multiFireCount == 0, $"Command count {_multiFireCount}  expected 0");


            // Wait  for last TestDomainEvent to be "heard" from logger/repo - times out because events not logged
            Assert.Throws<TrueException>(() => Assert.IsOrBecomesTrue(
                () => _testDomainEventCount > 0,
                1000,
                $"Found {_testDomainEventCount} TestDomainEvents on log, expected 0"));

            Assert.True(_testDomainEventCount == 0, $"Last event count {_testDomainEventCount} doesn't match expected value {1}");

            Assert.Throws<TrueException>(() => Assert.IsOrBecomesTrue(
                () => _testCommandCount == 1, 
                1000,
                $"Found {_testCommandCount} TestCommand3s on log - expected 0"));

            Assert.True(_testCommandCount == 0, $"Last event count {_testCommandCount} is not 0");
        }

        public void Handle(Message msg)
        {
            if (msg is TestCommands.TestCommand2) _multiFireCount++;
            if (msg is TestCommands.TestCommand3) _testCommandCount++;
            if (msg is CountedEvent) _countedEventCount++;
            if (msg is TestDomainEvent) _testDomainEventCount++;
        }
    }
}
