using ReactiveDomain.Foundation.EventStore;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using System;
using Xunit;
using Xunit.Sdk;

namespace ReactiveDomain.Foundation.Tests.Logging
{

    // ReSharper disable once InconsistentNaming
    [Collection(nameof(EmbeddedStreamStoreConnectionCollection))]
    public class when_logging_disabled_and_mixed_messages_are_published :
        with_message_logging_disabled,
        IHandle<Message>
    {
        static when_logging_disabled_and_mixed_messages_are_published()
        {
            BootStrap.Load();
        }

        
        private readonly CorrelationId _correlationId = CorrelationId.NewId();
        private IListener _listener;

        private readonly int _maxCountedEvents = 5;
        private int _countedEventCount;
        private int _testDomainEventCount;

        private int _multiFireCount;
        private int _testCommandCount;

        private readonly int _maxCountedMessages = 25;

        private TestCommandSubscriber _cmdHandler;


        public when_logging_disabled_and_mixed_messages_are_published(StreamStoreConnectionFixture fixture):base(fixture.Connection)
        {
        
            _countedEventCount = 0;
            _testDomainEventCount = 0;

            _cmdHandler = new TestCommandSubscriber(Bus);

            _multiFireCount = 0;
            _testCommandCount = 0;

            _listener = new SynchronizableStreamListener(
                Logging.FullStreamName, 
                Connection, 
                StreamNameBuilder,
                EventSerializer);
            _listener.EventStream.Subscribe<Message>(this);

            _listener.Start(Logging.FullStreamName);
            CorrelatedMessage source = CorrelatedMessage.NewRoot();
            // create and fire a mixed set of commands and events
            for (int i = 0; i < _maxCountedMessages; i++)
            {
                var evt = new CountedEvent(i, source);
                Bus.Publish(evt);
                
                // this is just an example command - choice to fire this one was random
                var cmd = new TestCommands.Command2(evt);

                Bus.Send(cmd,
                    $"exception message{i}",
                    TimeSpan.FromSeconds(2));
                source = cmd;
            }
           
            for (int i = 0; i < _maxCountedEvents; i++) {
                var evt = new TestEvent(source);
                Bus.Publish(evt);
                source = evt;
            }

            var tstCmd = new TestCommands.Command3(source);

            Bus.Send(tstCmd,
                "Test Command exception message",
                TimeSpan.FromSeconds(1));

        }



        public void mixed_messages_are_not_logged()
        {
            // all events published, commands fired
            Assert.IsOrBecomesTrue(()=> _cmdHandler.TestCommand3Handled >0);

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
            if (msg is TestCommands.Command2) _multiFireCount++;
            if (msg is TestCommands.Command3) _testCommandCount++;
            if (msg is CountedEvent) _countedEventCount++;
            if (msg is TestEvent) _testDomainEventCount++;
        }
    }
}
