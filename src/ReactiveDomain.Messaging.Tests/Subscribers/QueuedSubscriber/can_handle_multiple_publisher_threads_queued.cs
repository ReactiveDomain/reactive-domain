using System.Threading;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests.Subscribers.QueuedSubscriber {
    // ReSharper disable once InconsistentNaming
    // ReSharper disable once RedundantExtendsListEntry
    public sealed class can_handle_multiple_publisher_threads_queued {
        private class TestSubscriber : Bus.QueuedSubscriber {
            public TestSubscriber(IBus bus) : base(bus) { }
        }
        private int _count = 100;
        private int _msgCount;
        private long _cmdCount;

        [Fact]
        void can_handle_threaded_messages() {
            var bus = new Dispatcher("test", 2);
            using (var sub = new TestSubscriber(bus)) {
                sub.Subscribe(
                    new AdHocHandler<CountedTestMessage>(_ => Interlocked.Increment(ref _msgCount)));
                var messages = new IMessage[_count];
                for (int i = 0; i < _count; i++) {
                    messages[i] = new CountedTestMessage(i);
                }

                for (int i = 0; i < _count; i++) {
                    bus.Publish(messages[i]);
                }

                AssertEx.IsOrBecomesTrue(
                    () => _msgCount == _count,
                    1000,
                    $"Expected message count to be {_count} Messages, found {_msgCount }");

            }
        }

        [Fact]
        void can_handle_threaded_commands() {
            var bus = new Dispatcher("test", 2);
            using (var sub = new TestSubscriber(bus)) {
                sub.Subscribe(
                    new AdHocCommandHandler<TestCommands.OrderedCommand>(cmd => {
                        Interlocked.Increment(ref _cmdCount);
                        return Interlocked.Read(ref _cmdCount) == cmd.SequenceNumber + 1;
                    }));
                var messages = new TestCommands.OrderedCommand[_count];
                for (int i = 0; i < _count; i++) {
                    messages[i] = new TestCommands.OrderedCommand(i);
                }

                for (int i = 0; i < _count; i++) {
                    bus.Send(messages[i]);
                }

                AssertEx.IsOrBecomesTrue(
                    () => _cmdCount == _count,
                    1000,
                    $"Expected Command count to be {_count} Commands, found {_cmdCount }");

            }
        }

    }
}
