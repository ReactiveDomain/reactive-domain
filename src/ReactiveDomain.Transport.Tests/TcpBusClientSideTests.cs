using System;
using System.Net;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Transport.Tests
{
    public class TcpBusClientSideTests
    {
        private IPAddress _hostAddress;
        private IDispatcher _commandBus;
        private IPAddress _clientAddress;
        private MockTcpConnection _clientTcpConnection;
        private const int CommandPort = 10660;

        public TcpBusClientSideTests()
        {
            _commandBus = new Dispatcher("TestBus");
            _hostAddress = IPAddress.Loopback;
            _clientAddress = IPAddress.Loopback;
            _clientTcpConnection = MockTcpConnection.CreateConnectingTcpConnection(Guid.NewGuid(),
                new IPEndPoint(_hostAddress, CommandPort),
                new TcpClientConnector(),
                TimeSpan.FromSeconds(120),
                conn =>
                {
                },
                (conn, err) =>
                {
                },
                verbose: true);

        }
        ~TcpBusClientSideTests()
        {
            _clientTcpConnection.Close("I'm done.");
        }

        // Sigh... at this point, there are no commands defined in ReactiveDomain, so I have nothing with
        // which to test.  

        //[Fact]
        //public void handle_command_test()
        //{
        //    // Set up the TcpBusClientSide that I will test, and also hook up the LengthPrefixMessageFramer
        //    var tcpBusClientSide = new TcpBusClientSide(_hostAddress, _commandBus, _clientAddress, 10000, _clientTcpConnection);
        //    tcpBusClientSide._framer.RegisterMessageArrivedCallback(tcpBusClientSide.TcpMessageArrived);
        //    Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> callback = null;
        //    callback = (x, data) =>
        //    {
        //        tcpBusClientSide._framer.UnFrameData(data);
        //        _clientTcpConnection.ReceiveAsync(callback);
        //    };
        //    _clientTcpConnection.ReceiveAsync(callback);

        //    _clientTcpConnection.SentData = null;
        //    var cmd = new ImageProcess.Decolorize(true, Guid.NewGuid(), null);

        //    var cmdResponse = tcpBusClientSide.Handle(cmd);

        //    var expectedSentData = tcpBusClientSide._framer.FrameData((new TcpMessage(cmd).AsArraySegment()));
        //    var expectedCmdResponse = cmd.Succeed();
        //    Assert.NotNull(_clientTcpConnection.SentData);
        //    Assert.Equal(expectedSentData.ToArray(), _clientTcpConnection.SentData.ToArray());
        //    Assert.NotNull(cmdResponse);
        //    Assert.Equal(expectedCmdResponse.Succeeded, cmdResponse.Succeeded);
        //    Assert.Equal(expectedCmdResponse.Error, cmdResponse.Error);
        //}

    }
}
