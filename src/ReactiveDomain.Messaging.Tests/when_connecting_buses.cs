using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Messaging.Tests {
    // ReSharper disable once InconsistentNaming
    // ReSharper disable once UnusedMember.Global
    public class when_connecting_buses : IClassFixture<RemoteBusFixture> {
        private readonly RemoteBusFixture _fixture;


        public when_connecting_buses(RemoteBusFixture fixture) {
            _fixture = fixture;
        }
        [Fact]
        public void messages_will_be_published_on_both_buses_without_echo() {
            _fixture.Reset();
            _fixture.LocalBus.Publish(new WoftamEvent("who", "cares"));
            AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.LocalMsgCount) == 1);
            AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.RemoteMsgCount) == 1);
            AssertEx.IsOrBecomesTrue(() => _fixture.LocalBus.Idle && _fixture.RemoteBus.Idle);
            Assert.True(Interlocked.Read(ref _fixture.LocalMsgCount) == 1);
            Assert.True(Interlocked.Read(ref _fixture.RemoteMsgCount) == 1);
            _fixture.Reset();
            _fixture.RemoteBus.Publish(new WoftamEvent("who", "cares"));
            AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.LocalMsgCount) == 1);
            AssertEx.IsOrBecomesTrue(() => Interlocked.Read(ref _fixture.RemoteMsgCount) == 1);
            AssertEx.IsOrBecomesTrue(() => _fixture.RemoteBus.Idle && _fixture.LocalBus.Idle);
            Assert.True(Interlocked.Read(ref _fixture.LocalMsgCount) == 1);
            Assert.True(Interlocked.Read(ref _fixture.RemoteMsgCount) == 1);
        }
    }
}
