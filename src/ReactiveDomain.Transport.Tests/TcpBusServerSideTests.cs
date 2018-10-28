using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using Xunit;

namespace ReactiveDomain.Transport.Tests
{
    public class TcpBusServerSideTests
    {
        [Fact]
        public void can_handle_split_frames()
        {
            // 16kb large enough to cause the transport to split up the frame.
            // it would be better if we did the splitting manually so we were sure it really happened.
            // would require mocking more things.
            var hostAddress = IPAddress.Loopback;
            var prop1 = "prop1";
            var prop2 = string.Join("", Enumerable.Repeat("a", 16 * 1024));
            var port = 10000;
            var tcs = new TaskCompletionSource<Message>();

            // server side
            var serverInbound = new QueuedHandler(
                new AdHocHandler<Message>(tcs.SetResult),
                "InboundMessageQueuedHandler",
                true,
                TimeSpan.FromMilliseconds(1000));

            var tcpBusServerSide = new TcpBusServerSide(hostAddress, port, null)
            {
                InboundMessageQueuedHandler = serverInbound,
                InboundSpamMessageTypes = new List<Type>(),
            };

            serverInbound.Start();

            // client side
            var tcpBusClientSide = new TcpBusClientSide(null, hostAddress, port);

            // wait for tcp connection to be established (maybe an api to detect this would be nice)
            Thread.Sleep(TimeSpan.FromMilliseconds(200));

            // put message into client
            tcpBusClientSide.Handle(new WoftamEvent(prop1, prop2));

            // expect to receive it in the server
            var gotMessage = tcs.Task.Wait(TimeSpan.FromMilliseconds(1000));
            Assert.True(gotMessage);
            var evt = Assert.IsType<WoftamEvent>(tcs.Task.Result);
            Assert.Equal(prop1, evt.Property1);
            Assert.Equal(prop2, evt.Property2);
        }
    }
}
