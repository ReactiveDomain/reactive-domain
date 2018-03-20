using ReactiveDomain.Foundation.EventStore;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Messaging.Testing;
using ReactiveDomain.Testing;
using System;
using Xunit;
using Xunit.Sdk;

namespace ReactiveDomain.Foundation.Tests.Logging
{
    // ReSharper disable once InconsistentNaming
    [Collection(nameof(EventStoreCollection))]
    public class when_toggling_logging : 
        with_message_logging_enabled,
        IHandle<Message>
    {
        public when_toggling_logging(EmbeddedEventStoreFixture fixture):base(fixture.Connection)
        {
            
        }
        private readonly Guid _correlationId = Guid.NewGuid();
        private IListener _listener;
        private readonly int _maxCountedEvents = 5;
        private int _countedEventCount;
        private int _multiFireCount;

        private readonly int _maxCountedMessages = 25;

        private TestCommandSubscriber _cmdHandler;  // "never used" is a red herring. It handles the command


        protected override void When()
        {
            // command must have a commandHandler
            _cmdHandler = new TestCommandSubscriber(Bus);

            _multiFireCount = 0;

            _listener = new SynchronizableStreamListener(Logging.FullStreamName, Subscriber, StreamNameBuilder);

            _listener.Start(Logging.FullStreamName);

        }

        public void commands_logged_only_while_logging_is_enabled()
        {
            // create and fire a mixed set of commands and events
            for (int i = 0; i < _maxCountedMessages; i++)
            {
                // this is just an example command - choice to fire this one was random
                var cmd = new TestCommands.Command2(
                                        Guid.NewGuid(),
                                        null);
                Bus.Fire(cmd,
                    $"exception message{i}",
                    TimeSpan.FromSeconds(2));
            }

            Assert.IsOrBecomesTrue(
                () => _multiFireCount == _maxCountedMessages,
                1000,
               $"First set of Commands count {_multiFireCount} not properly logged. Expected {_maxCountedMessages}");

            Logging.Enabled = false;
            _multiFireCount = 0;

            // create and fire a mixed set of commands and events
            for (int i = 0; i < _maxCountedMessages; i++)
            {
                // this is just an example command - choice to fire this one was random
                var cmd = new TestCommands.Command2(
                                        Guid.NewGuid(),
                                        null);
                Bus.Fire(cmd,
                    $"exception message{i}",
                    TimeSpan.FromSeconds(2));
            }

            Assert.Throws<TrueException>(() => Assert.IsOrBecomesTrue(
                () => _multiFireCount > 0,
                5000,
                $"Found {_multiFireCount} of second set of commands on log. Should be 0"));

            Logging.Enabled = true;
            _multiFireCount = 0;

            for (int i = 0; i < _maxCountedMessages; i++)
            {
                // this is just an example command - choice to fire this one was random
                var cmd = new TestCommands.Command2(
                                        Guid.NewGuid(),
                                        null);
                Bus.Fire(cmd,
                    $"exception message{i}",
                    TimeSpan.FromSeconds(2));
            }

            var tstCmd = new TestCommands.Command3(
                Guid.NewGuid(),
                null);

            Bus.Fire(tstCmd,
                "Test Command exception message",
                TimeSpan.FromSeconds(1));

            TestQueue.WaitFor<TestCommands.Command3>(TimeSpan.FromSeconds(5));

            Assert.IsOrBecomesTrue(
                () => _multiFireCount == _maxCountedMessages,
                5000,
               $"First set of Commands count {_multiFireCount} doesn't match expected index {_maxCountedMessages}");

            Assert.True(
                _multiFireCount == _maxCountedMessages,
                $"Third set of Commands count {_multiFireCount} doesn't match expected index {_maxCountedMessages}");

        }

        public void events_logged_only_while_logging_is_enabled()
        {
            _countedEventCount = 0;

            // create and publish a set of events
            for (int i = 0; i < _maxCountedEvents; i++)
            {
                Bus.Publish(
                    new CountedEvent(i,
                        _correlationId,
                        Guid.NewGuid()));
            }

            Assert.IsOrBecomesTrue(
                () => _countedEventCount == _maxCountedEvents,
                1000,
               $"First set of Events count {_countedEventCount} - not properly logged");

            Logging.Enabled = false;
            _countedEventCount = 0;

            // publish again, with logging disabled
            for (int i = 0; i < _maxCountedEvents; i++)
            {
                Bus.Publish(
                    new CountedEvent(i,
                        _correlationId,
                        Guid.NewGuid()));
            }

            Assert.Throws<TrueException>(() => Assert.IsOrBecomesTrue(
                () => _countedEventCount > 0,
                5000,
                $"Found {_countedEventCount} of second set of events on log. Should be 0"));

            Logging.Enabled = true;
            _countedEventCount = 0;

            // publish again, with logging disabled
            for (int i = 0; i < _maxCountedEvents; i++)
            {
                Bus.Publish(
                    new CountedEvent(i,
                        _correlationId,
                        Guid.NewGuid()));
            }

            Assert.IsOrBecomesTrue(
                () => _countedEventCount == _maxCountedEvents,
                1000,
               $"Third set of Events count {_countedEventCount} - not properly logged");
        }

        public void mixed_messages_logged_only_while_logging_is_enabled()
        {
            _countedEventCount = 0;

            // create and publish a set of events and commands
            for (int i = 0; i < _maxCountedMessages; i++)
            {
                Bus.Publish(
                    new CountedEvent(i,
                        _correlationId,
                        Guid.NewGuid()));

                var cmd = new TestCommands.Command2(
                                        Guid.NewGuid(),
                                        null);

                Bus.Fire(cmd,
                    $"exception message{i}",
                    TimeSpan.FromSeconds(1));
            }

            Assert.IsOrBecomesTrue(
                () => _countedEventCount == _maxCountedMessages,
                1000,
               $"First set of Events count {_countedEventCount} - not properly logged");

            Assert.IsOrBecomesTrue(
                () => _multiFireCount == _maxCountedMessages,
                1000,
               $"First set of commands count {_multiFireCount} - not properly logged");

            Logging.Enabled = false;
            _countedEventCount = 0;
            _multiFireCount = 0;

            // repeat, with logging disabled
            for (int i = 0; i < _maxCountedMessages; i++)
            {
                Bus.Publish(
                    new CountedEvent(i,
                        _correlationId,
                        Guid.NewGuid()));

                var cmd = new TestCommands.Command2(
                                        Guid.NewGuid(),
                                        null);

                Bus.Fire(cmd,
                    $"exception message{i}",
                    TimeSpan.FromSeconds(1));
            }

            Assert.Throws<TrueException>(() => Assert.IsOrBecomesTrue(
                () => _countedEventCount > 0,
                5000,
                $"Found {_countedEventCount} of second set of messages on disabled log. Should be 0"));

            // hard to imagine getting here if the above fails...
            Assert.Throws<TrueException>(() => Assert.IsOrBecomesTrue(
                () => _multiFireCount > 0,
                5000,
                $"Found {_multiFireCount} of second set of messages on disabled log. Should be 0"));


            Logging.Enabled = true;
            _countedEventCount = 0;
            _multiFireCount = 0;

            // repeat, with logging enabled again
            for (int i = 0; i < _maxCountedMessages; i++)
            {
                Bus.Publish(
                    new CountedEvent(i,
                        _correlationId,
                        Guid.NewGuid()));

                var cmd = new TestCommands.Command2(
                                        Guid.NewGuid(),
                                        null);

                Bus.Fire(cmd,
                    $"exception message{i}",
                    TimeSpan.FromSeconds(1));
            }

            Assert.IsOrBecomesTrue(
                () => _countedEventCount == _maxCountedMessages,
                1000,
               $"Third set of Events count {_countedEventCount} - not properly logged");

            Assert.IsOrBecomesTrue(
                () => _multiFireCount == _maxCountedMessages,
                1000,
               $"Third set of commands count {_multiFireCount} - not properly logged");

        }



        public void Handle(Message msg)
        {
            if (msg is TestCommands.Command2) _multiFireCount++;

            if (msg is CountedEvent) _countedEventCount++;

        }
    }

}