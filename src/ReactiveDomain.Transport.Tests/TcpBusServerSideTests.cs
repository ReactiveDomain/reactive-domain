using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Testing;
using Xunit;

namespace ReactiveDomain.Transport.Tests
{
    [Collection("TCP bus tests")]
    public class TcpBusServerSideTests
    {
        private readonly IPAddress _hostAddress = IPAddress.Loopback;
#if NET6_0
        private int port = 10006; //net 6 and 8 will fight over the port in tests
#endif
#if NET8_0
        private int port = 10008; //net 6 and 8 will fight over the port in tests
#endif
        private readonly TaskCompletionSource<IMessage> _tcs = new TaskCompletionSource<IMessage>();

        [Fact]
        public void can_handle_split_frames()
        {
            // 16kb large enough to cause the transport to split up the frame.
            // it would be better if we did the splitting manually so we were sure it really happened.
            // would require mocking more things.
            var prop1 = "prop1";
            var prop2 = string.Join("", Enumerable.Repeat("a", 16 * 1024));

            // server side
            var serverInbound = new QueuedHandler(
                new AdHocHandler<IMessage>(_tcs.SetResult),
                "InboundMessageQueuedHandler",
                true,
                TimeSpan.FromMilliseconds(1000));

            var tcpBusServerSide = new TcpBusServerSide(
                _hostAddress,
                port,
                inboundNondiscardingMessageTypes: new[] { typeof(WoftamEvent) },
                inboundNondiscardingMessageQueuedHandler: serverInbound);

            serverInbound.Start();

            // client side
            var tcpBusClientSide = new TcpBusClientSide(_hostAddress, port);

            // wait for tcp connection to be established
            AssertEx.IsOrBecomesTrue(() => tcpBusClientSide.IsConnected, 200);

            // put message into client
            tcpBusClientSide.Handle(new WoftamEvent(prop1, prop2));

            // expect to receive it in the server
            var gotMessage = _tcs.Task.Wait(TimeSpan.FromMilliseconds(1000));
            Assert.True(gotMessage);
            var evt = Assert.IsType<WoftamEvent>(_tcs.Task.Result);
            Assert.Equal(prop1, evt.Property1);
            Assert.Equal(prop2, evt.Property2);

            tcpBusClientSide.Dispose();
            tcpBusServerSide.Dispose();
        }

        [Fact]
        public void can_filter_out_message_types()
        {
            // server side
            var serverInbound = new QueuedHandler(
                new AdHocHandler<IMessage>(_tcs.SetResult),
                "InboundMessageQueuedHandler",
                true,
                TimeSpan.FromMilliseconds(1000));

            var tcpBusServerSide = new TcpBusServerSide(
                _hostAddress,
                port,
                inboundNondiscardingMessageTypes: new[] { typeof(WoftamEvent) },
                inboundNondiscardingMessageQueuedHandler: serverInbound);

            serverInbound.Start();

            // client side
            var tcpBusClientSide = new TcpBusClientSide(_hostAddress, port);

            // wait for tcp connection to be established
            AssertEx.IsOrBecomesTrue(() => tcpBusClientSide.IsConnected, 200);

            // put disallowed message into client
            tcpBusClientSide.Handle(new WoftamCommand("abc"));

            // expect to receive it in the server but drop it on the floor
            var gotMessage = _tcs.Task.Wait(TimeSpan.FromMilliseconds(1000));
            Assert.False(gotMessage);

            tcpBusClientSide.Dispose();
            tcpBusServerSide.Dispose();
        }
    }
}
