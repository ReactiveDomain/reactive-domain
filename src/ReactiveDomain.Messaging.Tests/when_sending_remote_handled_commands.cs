using System.Threading;
using System.Threading.Tasks;
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
                var ts1 = new CancellationTokenSource();
                CommandResponse response1 = null;
                var t1 = Task.Run(()=> Assert.False(_fixture.LocalBus.TrySend(new TestCommands.RemoteCancel(ts1.Token), out response1)), TestContext.Current.CancellationToken);
                ts1.Cancel();

                var ts2 = new CancellationTokenSource();
                CommandResponse response2 = null;
                var t2 = Task.Run(() => Assert.False(_fixture.RemoteBus.TrySend(new TestCommands.RemoteCancel(ts2.Token), out response2)), TestContext.Current.CancellationToken);
                ts2.Cancel();

                AssertEx.EnsureComplete(t1, t2);
                Assert.NotNull(response1);
                Assert.NotNull(response2);
                Assert.IsType<Canceled>(response1);
                Assert.IsType<Canceled>(response2);
            }
        }

    }
}
