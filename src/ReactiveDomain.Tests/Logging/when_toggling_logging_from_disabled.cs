using System;
using ReactiveDomain.Legacy;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Tests.Helpers;
using ReactiveDomain.Tests.Subscribers.QueuedSubscriber;
using Xunit;
using Xunit.Sdk;

namespace ReactiveDomain.Tests.Logging
{
    // ReSharper disable once InconsistentNaming
    public class when_toggling_logging_from_disabled :
        with_message_logging_disabled,
        IHandle<Message>
    {
        private readonly Guid _correlationId = Guid.NewGuid();
        private IListener _listener;
        private readonly int _maxCountedEvents = 5;
        private int _countedEventCount;
        private int _multiFireCount;

        private readonly int _maxCountedMessages = 6;

        private TestCommandSubscriber _cmdHandler; // "never used" is a red herring. It handles the command


        protected override void When()
        {
            // command must have a commandHandler
            _cmdHandler = new TestCommandSubscriber(Bus);

            _multiFireCount = 0;

            _listener = Repo.GetListener(Logging.FullStreamName);
            _listener.EventStream.Subscribe<Message>(this);

            _listener.Start(Logging.FullStreamName);

        }

        [Fact(Skip = SkipReason)]
        public void commands_logged_only_while_logging_is_enabled()
        {
            Assert.False(Logging.Enabled);

            _multiFireCount = 0;

            for (int i = 0; i < _maxCountedMessages; i++)
            {
                // this is just an example command - choice to fire this one was random
                var cmd = new TestCommands.TestCommand2(
                                        Guid.NewGuid(),
                                        null);
                Bus.Fire(cmd,
                    $"exception message{i}",
                    TimeSpan.FromSeconds(2));
            }

            Assert.Throws<TrueException>(() => Assert.IsOrBecomesTrue(
                () => _multiFireCount > 0,
                1000,
                 $"Found {_multiFireCount} of first set of commands on disabled log. Should be 0"));


            _multiFireCount = 0;
            Logging.Enabled = true;

            // create and fire a mixed set of commands and events
            for (int i = 0; i < _maxCountedMessages; i++)
            {
                // this is just an example command - choice to fire this one was random
                var cmd = new TestCommands.TestCommand2(
                                        Guid.NewGuid(),
                                        null);
                Bus.Fire(cmd,
                    $"exception message{i}",
                    TimeSpan.FromSeconds(2));
            }

            Assert.IsOrBecomesTrue(
                () => _multiFireCount == _maxCountedMessages,
                1000,
                $"Second set of Commands count {_multiFireCount} not properly logged. Expected {_maxCountedMessages}");


            Logging.Enabled = false;
            _multiFireCount = 0;

            for (int i = 0; i < _maxCountedMessages; i++)
            {
                // this is just an example command - choice to fire this one was random
                var cmd = new TestCommands.TestCommand2(
                                        Guid.NewGuid(),
                                        null);
                Bus.Fire(cmd,
                    $"exception message{i}",
                    TimeSpan.FromSeconds(2));
            }

            Assert.Throws<TrueException>(() => Assert.IsOrBecomesTrue(
                () => _multiFireCount > 0,
                1000,
                $"Found {_multiFireCount} of third set of commands on disabled log. Should be 0"));


        }

        [Fact(Skip = SkipReason)]
        public void events_logged_only_while_logging_is_enabled()
        {
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
                1000,
                $"Found {_countedEventCount} of first set of events on log. Should be 0"));

            Logging.Enabled = true;
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
                $"Second set of Events count {_countedEventCount} - not properly logged");

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
                1000,
                $"Found {_countedEventCount} of third set of events on log. Should be 0"));
        }

        [Fact(Skip = SkipReason)]
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

                var cmd = new TestCommands.TestCommand2(
                                        Guid.NewGuid(),
                                        null);
            }

            Assert.Throws<TrueException>(() => Assert.IsOrBecomesTrue(
                () => _countedEventCount > 0,
                1000,
                $"Found {_countedEventCount} of first set of events on log. Should be 0"));

            Assert.Throws<TrueException>(() => Assert.IsOrBecomesTrue(
                () => _multiFireCount > 0,
                1000,
                $"First set of commands count {_multiFireCount} - not properly logged"));

            Logging.Enabled = true;
            _countedEventCount = 0;
            _multiFireCount = 0;

            // repeat, with logging disabled
            for (int i = 0; i < _maxCountedMessages; i++)
            {
                Bus.Publish(
                    new CountedEvent(i,
                        _correlationId,
                        Guid.NewGuid()));

                var cmd = new TestCommands.TestCommand2(
                    Guid.NewGuid(),
                    null);

                Bus.Fire(cmd,
                    $"exception message{i}",
                    TimeSpan.FromSeconds(1));
            }

            Assert.IsOrBecomesTrue(
                () => _countedEventCount == _maxCountedMessages,
                1000,
                $"Second set of Events count {_countedEventCount} - not properly logged");


            Assert.IsOrBecomesTrue(
                () => _multiFireCount == _maxCountedMessages,
                1000,
                $"Second set of commands count {_multiFireCount} - not properly logged");

            Logging.Enabled = false;
            _countedEventCount = 0;
            _multiFireCount = 0;

            // repeat, with logging enabled again
            for (int i = 0; i < _maxCountedMessages; i++)
            {
                Bus.Publish(
                    new CountedEvent(i,
                        _correlationId,
                        Guid.NewGuid()));

                var cmd = new TestCommands.TestCommand2(
                    Guid.NewGuid(),
                    null);

                Bus.Fire(cmd,
                    $"exception message{i}",
                    TimeSpan.FromSeconds(1));
            }

            Assert.Throws<TrueException>(() => Assert.IsOrBecomesTrue(
                () => _countedEventCount > 0,
                1000,
                $"Found {_countedEventCount} of Third set of events on disabled log. Should be 0"));

            Assert.Throws<TrueException>(() => Assert.IsOrBecomesTrue(
                () => _multiFireCount > 0,
                1000,
                $"Third set of commands count {_multiFireCount} - not properly logged"));

        }



        public void Handle(Message msg)
        {
            if (msg is TestCommands.TestCommand2)
                _multiFireCount++;

            if (msg is CountedEvent)
                _countedEventCount++;

        }
    }

}