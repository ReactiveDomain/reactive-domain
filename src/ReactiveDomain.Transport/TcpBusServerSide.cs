using System;
using System.Collections.Generic;
using System.Net;
using ReactiveDomain.Transport.Framing;
using ReactiveDomain.Messaging.Bus;

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
                TcpConnection = Transport.TcpConnection.CreateAcceptedTcpConnection(Guid.NewGuid(), endPoint, socket, verbose: true);

                Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> callback = null;
                callback = (x, data) =>
                {
                    try
                    {
                        framer.UnFrameData(data);
                    }
                    catch (PackageFramingException exc)
                    {
                        Log.ErrorException(exc, "LengthPrefixMessageFramer.UnFrameData() threw an exception:");
                        // SendBadRequestAndClose(Guid.Empty, string.Format("Invalid TCP frame received. Error: {0}.", exc.Message));
                        return;
                    }
                    TcpConnection.ReceiveAsync(callback);
                };
                TcpConnection.ReceiveAsync(callback);
            }, "Standard");
            Log.Info("ConfigureTcpListener(" + hostEndPoint.AddressFamily + ", " + hostEndPoint.Port + ") successfully constructed TcpServerListener.");
            return listener;
        }

    }
}
