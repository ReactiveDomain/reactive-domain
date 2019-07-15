using System.Threading;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests {
    // ReSharper disable once InconsistentNaming
    // ReSharper disable once UnusedMember.Global
    public class when_sending_remote_handled_commands : IClassFixture<RemoteBusFixture> {
        private readonly RemoteBusFixture _fixture;

        public when_sending_remote_handled_commands(RemoteBusFixture fixture) {
            _fixture = fixture;
        }

        [Fact]
        public void can_handle_commands_from_either_bus() {
            using (_fixture.RemoteBus.Subscribe(new AdHocCommandHandler<TestCommands.RemoteHandled>(_ => true))) {
                Assert.True(_fixture.LocalBus.TrySend(new TestCommands.RemoteHandled(), out _));
                Assert.True(_fixture.RemoteBus.TrySend(new TestCommands.RemoteHandled(), out _));
            }

        }

        [Fact]
        public void can_fail_commands_from_either_bus() {
            using (_fixture.RemoteBus.Subscribe(new AdHocCommandHandler<TestCommands.RemoteHandled>(_ => false))) {
                Assert.False(_fixture.LocalBus.TrySend(new TestCommands.RemoteHandled(), out var response));
                Assert.IsType<Fail>(response);
                Assert.False(_fixture.RemoteBus.TrySend(new TestCommands.RemoteHandled(), out response));
                Assert.IsType<Fail>(response);
            }
        }
        [Fact]
        public void can_cancel_commands_from_either_bus() {
            using (_fixture.RemoteBus.Subscribe(new AdHocCommandHandler<TestCommands.RemoteCancel>(
                    cmd => {
                        SpinWait.SpinUntil(() => cmd.IsCanceled);
                        return false;}))
                    ) 
            {
                var ts = new CancellationTokenSource(5);
                Assert.False(_fixture.LocalBus.TrySend(new TestCommands.RemoteCancel(ts.Token), out var response));
                Assert.IsType<Canceled>(response);
                ts = new CancellationTokenSource(5);
                Assert.False(_fixture.RemoteBus.TrySend(new TestCommands.RemoteCancel(ts.Token), out response));
                Assert.IsType<Canceled>(response);
            }
        }

    }
}
