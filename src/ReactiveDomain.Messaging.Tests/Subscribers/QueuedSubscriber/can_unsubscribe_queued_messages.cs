using System;
using System.Collections.Generic;
using System.Threading;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests.Subscribers.QueuedSubscriber {
    // ReSharper disable once InconsistentNaming
    public sealed class can_unsubscribe_queued_messages {

        private readonly IDispatcher _bus = new Dispatcher("test", 3);
        private int _msgCount = 20;
        private readonly List<IMessage> _messages = new List<IMessage>();
        private int _messageCount;
        private int _eventCount;
        private long _cmdCount;

        private class TestSubscriber : Bus.QueuedSubscriber {
            public TestSubscriber(IBus bus) : base(bus) { }
        }

        public can_unsubscribe_queued_messages() {
           
            for (var i = 0; i < _msgCount; i++) {
                _messages.Add(new CountedTestMessage(i));
                var evt = new CountedEvent(i);
                _messages.Add(evt);
                var cmd = new TestCommands.OrderedCommand(i);
                _messages.Add(cmd);              
            }
        }
        [Fact]
        void can_unsubscribe_messages_commands_and_events_by_disposing() {
            using (var sub = new TestSubscriber(_bus)) {
                sub.Subscribe(new AdHocHandler<CountedTestMessage>(_ => Interlocked.Increment(ref _messageCount)));
                sub.Subscribe(new AdHocHandler<CountedEvent>(_ => Interlocked.Increment(ref _eventCount)));
                sub.Subscribe(new AdHocCommandHandler<TestCommands.OrderedCommand>(
                    cmd => {
                        Interlocked.Increment(ref _cmdCount);
                        return Interlocked.Read(ref _cmdCount) == cmd.SequenceNumber + 1;
                    }));

                foreach (var msg in _messages) {
                    if (msg is TestCommands.OrderedCommand command) {
                        _bus.Send(command);
                    }
                    else {
                        _bus.Publish(msg);
                    }
                }
                AssertEx.IsOrBecomesTrue(() => _messageCount == _msgCount);
                AssertEx.IsOrBecomesTrue(() => _eventCount == _msgCount);
                AssertEx.IsOrBecomesTrue(() => _cmdCount == _msgCount);

            }

            for (int i = 0; i < 3; i++) {
                var msg = _messages[i];
                if(msg is TestCommands.OrderedCommand command){
                   Assert.Throws<CommandNotHandledException>(()=>_bus.Send(command));
                }
                else{
                    _bus.Publish(msg);
                }
            }

            Assert.True(_messageCount == _msgCount);
            Assert.True(_eventCount == _msgCount);
            Assert.True(_cmdCount == _msgCount);
        }
    }
}
