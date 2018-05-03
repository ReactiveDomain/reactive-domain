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
    public class when_toggling_logging_from_disabled :
        with_message_logging_disabled,
        IHandle<Message>
    {
        private IListener _listener;
        private readonly int _maxCountedEvents = 5;
        private int _countedEventCount;
        private int _multiFireCount;

        private readonly int _maxCountedMessages = 6;

        private TestCommandSubscriber _cmdHandler; // "never used" is a red herring. It handles the command


        public when_toggling_logging_from_disabled(StreamStoreConnectionFixture fixture):base(fixture.Connection)
        {
          
        
            // command must have a commandHandler
            _cmdHandler = new TestCommandSubscriber(Bus);

            _multiFireCount = 0;

            _listener = new SynchronizableStreamListener(
                Logging.FullStreamName, 
                Connection, 
                StreamNameBuilder,
                EventSerializer);
            _listener.EventStream.Subscribe<Message>(this);

            _listener.Start(Logging.FullStreamName);

        }

        public void commands_logged_only_while_logging_is_enabled()
        {
            Assert.False(Logging.Enabled);

            _multiFireCount = 0;
            CorrelatedMessage source = CorrelatedMessage.NewRoot();
            for (int i = 0; i < _maxCountedMessages; i++)
            {
                var cmd = new TestCommands.Command2(source);
                Bus.Send(cmd,
                    $"exception message{i}",
                    TimeSpan.FromSeconds(2));
                source = cmd;
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
                var cmd = new TestCommands.Command2(source);
                Bus.Send(cmd,
                    $"exception message{i}",
                    TimeSpan.FromSeconds(2));
                source = cmd;
            }

            Assert.IsOrBecomesTrue(
                () => _multiFireCount == _maxCountedMessages,
                1000,
                $"Second set of Commands count {_multiFireCount} not properly logged. Expected {_maxCountedMessages}");


            Logging.Enabled = false;
            _multiFireCount = 0;

            for (int i = 0; i < _maxCountedMessages; i++)
            {   
                var cmd = new TestCommands.Command2(source);
                Bus.Send(cmd,
                    $"exception message{i}",
                    TimeSpan.FromSeconds(2));
                source = cmd;
            }

            Assert.Throws<TrueException>(() => Assert.IsOrBecomesTrue(
                () => _multiFireCount > 0,
                1000,
                $"Found {_multiFireCount} of third set of commands on disabled log. Should be 0"));


        }

        public void events_logged_only_while_logging_is_enabled()
        {
            _countedEventCount = 0;
            CorrelatedMessage source = CorrelatedMessage.NewRoot();
            // publish again, with logging disabled
            for (int i = 0; i < _maxCountedEvents; i++)
            {
                var evt = new CountedEvent(i, source);
                Bus.Publish(evt);
                source = evt;
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
                var evt = new CountedEvent(i, source);
                Bus.Publish(evt);
                source = evt;
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
                var evt = new CountedEvent(i, source);
                Bus.Publish(evt);
                source = evt;
            }

            Assert.Throws<TrueException>(() => Assert.IsOrBecomesTrue(
                () => _countedEventCount > 0,
                1000,
                $"Found {_countedEventCount} of third set of events on log. Should be 0"));
        }

        public void mixed_messages_logged_only_while_logging_is_enabled()
        {
            _countedEventCount = 0;
            CorrelatedMessage source = CorrelatedMessage.NewRoot();
            // create and publish a set of events and commands
            for (int i = 0; i < _maxCountedMessages; i++)
            {
                var evt = new CountedEvent(i, source);
                Bus.Publish(evt);
                var cmd = new TestCommands.Command2(evt);
                Bus.Send(cmd,$"exception message{i}",TimeSpan.FromSeconds(1));
                source = cmd;
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
                var evt = new CountedEvent(i, source);
                Bus.Publish(evt);

                var cmd = new TestCommands.Command2(evt);
                Bus.Send(cmd,$"exception message{i}",TimeSpan.FromSeconds(1));
                source = cmd;
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
                var evt = new CountedEvent(i, source);
                Bus.Publish(evt);
                var cmd = new TestCommands.Command2(evt);
                Bus.Send(cmd,$"exception message{i}",TimeSpan.FromSeconds(1));
                source = cmd;
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
            if (msg is TestCommands.Command2)
                _multiFireCount++;

            if (msg is CountedEvent)
                _countedEventCount++;

        }
    }

}