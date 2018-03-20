using ReactiveDomain.Foundation.EventStore;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Messaging.Testing;
using ReactiveDomain.Testing;
using System;
using Xunit;

namespace ReactiveDomain.Foundation.Tests.Logging
{
    // ReSharper disable once InconsistentNaming
    [Collection(nameof(EventStoreCollection))]
    public class when_logging_high_volume_message_traffic :
        with_message_logging_enabled,
        IHandle<Message>
    {
        private readonly Guid _correlationId = Guid.NewGuid();
        private IListener _listener;

        public when_logging_high_volume_message_traffic(EmbeddedEventStoreFixture fixture):base(fixture.Connection)
        {
            
        }
        private readonly int _maxCountedMessages = 10000;

        private int _commandFireCount;
        private int _commandAckCount;
        private int _commandSuccessCount;
        private int _lastCommandCount;
        private int _countedEventCount;
        private int _testDomainEventCount;
        private int _numberOfItemsLogged;
        private int _catchupSubscriptionMsgs;

        private TestCommandSubscriber _cmdHandler;
        protected override void When()
        {
            // commands must have a commandHandler
            _cmdHandler = new TestCommandSubscriber(Bus);

             _commandFireCount = 0;
             _commandAckCount = 0;
             _commandSuccessCount = 0;
             _lastCommandCount = 0;
             _countedEventCount = 0;
             _testDomainEventCount = 0;
             _numberOfItemsLogged = 0;
             _catchupSubscriptionMsgs = 0;

            _listener = new SynchronizableStreamListener(Logging.FullStreamName, Subscriber, StreamNameBuilder);
            _listener.EventStream.Subscribe<Message>(this);

            _listener.Start(Logging.FullStreamName);

            // create and fire a mixed set of commands and events
            for (int i = 0; i < _maxCountedMessages; i++)
            {
                Bus.Publish(
                    new CountedEvent(i,
                        Guid.NewGuid(),
                        Guid.NewGuid()));

                // this is just an example command - choice to fire this one was random
                var cmd = new TestCommands.Command2(
                                        Guid.NewGuid(),
                                        null);
                Bus.Fire(cmd,
                    $"exception message{i}",
                    TimeSpan.FromSeconds(2));

                Bus.Publish(new TestDomainEvent(Guid.NewGuid(), Guid.NewGuid()));

            }

            var tstCmd = new TestCommands.Command3(
                        _correlationId,
                        null);

            Bus.Fire(tstCmd,
                "TestCommand3 failed",
                TimeSpan.FromSeconds(2));

        }

        
        public void all_messages_are_logged()
        {
            // Wait for last command to be queued
            TestQueue.WaitFor<TestCommands.Command3>(TimeSpan.FromSeconds(10));

            // Wait  for last command to be "heard" from logger/repo
            Assert.IsOrBecomesTrue(() => 
                _lastCommandCount == 1, 
                40000,
                $"Last command count = {_lastCommandCount}. Command never handled");

            // Wait  for last CountedEvent to be "heard" from logger/repo
            Assert.IsOrBecomesTrue(
                () => _countedEventCount == _maxCountedMessages, 
                2000,
                $"Message {_countedEventCount} doesn't attain expected index {_maxCountedMessages}");

            Assert.True(_countedEventCount == _maxCountedMessages, $"Message {_countedEventCount} doesn't match expected index {_maxCountedMessages}");

            // Wait  for last TestCommand2 to be "heard" from logger/repo
            Assert.IsOrBecomesTrue(() => _commandFireCount == _maxCountedMessages,
                3000,
                 $"Command count {_commandFireCount} doesn't attain expected index {_maxCountedMessages}");

            Assert.True(_commandFireCount == _maxCountedMessages, $"Command count {_commandFireCount} doesn't match expected index {_maxCountedMessages}");

            // Wait  for last TestDomainEvent to be "heard" from logger/repo
            Assert.IsOrBecomesTrue(() => _testDomainEventCount == _maxCountedMessages,
                1000,
                $"Last event count {_testDomainEventCount} doesn't attain expected value {_maxCountedMessages}");

            Assert.True(_testDomainEventCount == _maxCountedMessages, $"Last event count {_testDomainEventCount} doesn't match expected value {1}");


            Assert.True(_lastCommandCount == 1, $"Last command count {_lastCommandCount} doesn't match expected value, 1");

            // were all expected items logged? 
            //Note that for a command, there is the command itself, the AckCommand and Success
            var sumOfItemsLogged = _commandFireCount +
                                    _countedEventCount + 
                                    _testDomainEventCount +
                                   _lastCommandCount + 
                                   _commandAckCount +
                                   _commandSuccessCount +
                                   _catchupSubscriptionMsgs;

            Assert.IsOrBecomesTrue(() =>
                    _numberOfItemsLogged == sumOfItemsLogged,
                    500,
                    $"Number of items logged  {_numberOfItemsLogged} doesn't match expected value, {sumOfItemsLogged}");
        }

        public void Handle(Message msg)
        {
            if (msg is TestCommands.Command2)
                _commandFireCount++;
            else if (msg is TestCommands.Command3)
                _lastCommandCount++;
            else if(msg is CountedEvent)
                _countedEventCount++;
            else if(msg is TestDomainEvent)
                _testDomainEventCount++;
            else if(msg is Success)
                _commandSuccessCount++;
            else if (msg is AckCommand)
                _commandAckCount++;
            if (! (msg is EventStoreMsg.CatchupSubscriptionBecameLive))
                _numberOfItemsLogged++;
        }
    }
}