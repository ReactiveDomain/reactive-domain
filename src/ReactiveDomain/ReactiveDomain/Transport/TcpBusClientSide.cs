using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using ReactiveDomain.Transport.Framing;
using ReactiveDomain.Bus;

namespace ReactiveDomain.Transport
{
    public class TcpBusClientSide : TcpBusSide

    {

        public TcpBusClientSide(
            IGeneralBus messageBus,
            IPAddress hostIP,
            int commandPort,
            ITcpConnection tcpConnection = null)
            : base(hostIP, commandPort, messageBus)
        {

            _tcpConnection = tcpConnection ?? CreateTcpConnection(CommandEndpoint);
        }

        private ITcpConnection CreateTcpConnection(IPEndPoint endPoint)
        {
            Log.Info("TcpBusClientSide.CreateTcpConnection(" + endPoint.Address + ":" + endPoint.Port + ") entered.");
            var clientTcpConnection = TcpConnection.CreateConnectingTcpConnection(Guid.NewGuid(),
                endPoint,
                new TcpClientConnector(),
                TimeSpan.FromSeconds(120),
                conn =>
                {
                    Log.Info("TcpBusClientSide.CreateTcpConnection(" + endPoint.Address + ":" + endPoint.Port + ") successfully constructed TcpConnection.");

                    ConfigureTcpListener();
                },
                (conn, err) =>
                {
                    HandleError(conn, err);
                },
                verbose: true);

            return clientTcpConnection;
        }

        private void HandleError(ITcpConnection conn, SocketError err)
        {
            // assume that any connection error means that the Host isn't running, yet.  Just wait
            // a second and try again.
            Thread.Sleep(1000);
            Log.Debug("TcpBusClientSide call to CreateConnectingTcpConnection() failed - SocketError= " + err + " - retrying.");
            _tcpConnection = CreateTcpConnection(CommandEndpoint);
        }


        private void ConfigureTcpListener()
        {
            _framer.RegisterMessageArrivedCallback(TcpMessageArrived);
            Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> callback = null;
            callback = (x, data) =>
            {
                try
                {
                    _framer.UnFrameData(data);
                }
                catch (PackageFramingException exc)
                {
                    Log.Error(exc, "LengthPrefixMessageFramer.UnFrameData() threw an exception:");
                    // SendBadRequestAndClose(Guid.Empty, string.Format("Invalid TCP frame received. Error: {0}.", exc.Message));
                    return;
                }
                _tcpConnection.ReceiveAsync(callback);
            };
            _tcpConnection.ReceiveAsync(callback);
        }

    }
}
