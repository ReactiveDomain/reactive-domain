using System;
using System.Collections.Generic;
using System.Net;
using ReactiveDomain.Transport.Framing;
using ReactiveDomain.Bus;

namespace ReactiveDomain.Transport
{
    public class TcpBusServerSide : TcpBusSide
    {
        private readonly TcpServerListener _commandPortListener;

        public TcpBusServerSide(
            IPAddress hostIp,
            int commandPort,
            IGeneralBus messageBus)
            : base(hostIp, commandPort, messageBus)
        {           
            _commandPortListener = ConfigureTcpListener(CommandEndpoint, TcpMessageArrived);
        }       

        private TcpServerListener ConfigureTcpListener(IPEndPoint hostEndPoint, Action<ArraySegment<byte>> handler)
        {
            Log.Info("ConfigureTcpListener(" + hostEndPoint.AddressFamily + ", " + hostEndPoint.Port + ") entered.");
          
            LengthPrefixMessageFramer framer = new LengthPrefixMessageFramer();
            framer.RegisterMessageArrivedCallback(handler);

            var listener = new TcpServerListener(hostEndPoint);

            listener.StartListening((endPoint, socket) =>
            {
                _tcpConnection = TcpConnection.CreateAcceptedTcpConnection(Guid.NewGuid(), endPoint, socket, verbose: true);

                Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> callback = null;
                callback = (x, data) =>
                {
                    try
                    {
                        framer.UnFrameData(data);
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
            }, "Standard");
            Log.Info("ConfigureTcpListener(" + hostEndPoint.AddressFamily + ", " + hostEndPoint.Port + ") successfully constructed TcpServerListener.");
            return listener;
        }

    }
}
