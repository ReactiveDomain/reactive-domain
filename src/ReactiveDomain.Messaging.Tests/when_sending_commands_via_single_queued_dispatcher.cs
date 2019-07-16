using System.Threading;
using System.Threading.Tasks;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests {
    // ReSharper disable once InconsistentNaming
    // ReSharper disable once UnusedMember.Global
    public class when_sending_commands_via_single_queued_dispatcher :
        IHandleCommand<TestCommands.Command1>,
        IHandle<AckCommand>,
        IHandle<CommandResponse> {
        private long _gotAck;
        private long _gotMessage;
        private long _releaseAckHandler;
        private long _commandHandleStarted;
        private long _releaseHandler;
        private long _gotResponse;

        private readonly Dispatcher _dispatcher;



        public when_sending_commands_via_single_queued_dispatcher() {
            _dispatcher = new Dispatcher("test", 1);
            // ReSharper disable once RedundantTypeArgumentsOfMethod
            _dispatcher.Subscribe<TestCommands.Command1>(this);
            _dispatcher.Subscribe<AckCommand>(this);
            _dispatcher.Subscribe<CommandResponse>(this);
        }

        [Fact]
        public void send_cmd_msg_sequence_is_correct() {
            Task.Run(() => _dispatcher.Send(new TestCommands.Command1()));
            AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _gotAck) == 1);
            Assert.True(_commandHandleStarted == 0);
            Interlocked.Increment(ref _releaseAckHandler);
            AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _commandHandleStarted) == 1);
            Assert.True(_gotResponse == 0);
            Interlocked.Increment(ref _releaseHandler);
            AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _gotResponse) == 1);
        }
        [Fact]
        public void send_async_cmd_msg_sequence_is_correct() {
            _dispatcher.TrySendAsync(new TestCommands.Command1());
            AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _gotAck) == 1);
            Assert.True(_commandHandleStarted == 0);
            Interlocked.Increment(ref _releaseAckHandler);
            AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _commandHandleStarted) == 1);
            Assert.True(_gotResponse == 0);
            Interlocked.Increment(ref _releaseHandler);
            AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _gotResponse) == 1);
        }

        CommandResponse IHandleCommand<TestCommands.Command1>.Handle(TestCommands.Command1 cmd) {
            Interlocked.Increment(ref _commandHandleStarted);
            SpinWait.SpinUntil(() => Interlocked.Read(ref _releaseHandler) == 1);
            return cmd.Succeed();
        }

        void IHandle<AckCommand>.Handle(AckCommand ack) {
            Interlocked.Increment(ref _gotAck);
            SpinWait.SpinUntil(() => Interlocked.Read(ref _releaseAckHandler) == 1);
        }

        void IHandle<CommandResponse>.Handle(CommandResponse response) {
            Interlocked.Increment(ref _gotResponse);
        }

    }
}
