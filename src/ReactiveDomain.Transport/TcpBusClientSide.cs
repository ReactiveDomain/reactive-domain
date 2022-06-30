using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using ReactiveDomain.Messaging;
using ReactiveDomain.Transport.Framing;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Transport.Serialization;
using ReactiveDomain.Util;

namespace ReactiveDomain.Transport
{
    public sealed class TcpBusClientSide : TcpBus
    {
        public bool IsConnected { get; private set; }

        public TcpBusClientSide(
            EndPoint endpoint,
            IEnumerable<Type> inboundDiscardingMessageTypes,
            QueuedHandlerDiscarding inboundDiscardingMessageHandler,
            IEnumerable<Type> inboundNondiscardingMessageTypes,
            QueuedHandler inboundNondiscardingMessageHandler,
            ITcpConnection tcpConnection = null,
            Dictionary<Type, IMessageSerializer> messageSerializers = null)
            : base(
                endpoint,
                inboundDiscardingMessageTypes,
                inboundDiscardingMessageHandler,
                inboundNondiscardingMessageTypes,
                inboundNondiscardingMessageHandler,
                messageSerializers)
        {
            AddConnection(tcpConnection ?? CreateTcpConnection(CommandEndpoint));
        }

        public TcpBusClientSide(
            IPAddress hostIP,
            int commandPort,
            IEnumerable<Type> inboundDiscardingMessageTypes = null,
            QueuedHandlerDiscarding inboundDiscardingMessageQueuedHandler = null,
            IEnumerable<Type> inboundNondiscardingMessageTypes = null,
            QueuedHandler inboundNondiscardingMessageQueuedHandler = null,
            ITcpConnection tcpConnection = null,
            Dictionary<Type, IMessageSerializer> messageSerializers = null)
            : base(
                hostIP,
                commandPort,
                inboundDiscardingMessageTypes,
                inboundDiscardingMessageQueuedHandler,
                inboundNondiscardingMessageTypes,
                inboundNondiscardingMessageQueuedHandler,
                messageSerializers)
        {
            AddConnection(tcpConnection ?? CreateTcpConnection(CommandEndpoint));
        }

        private ITcpConnection CreateTcpConnection(EndPoint endPoint)
        {
            Log.Info($"TcpBusClientSide.CreateTcpConnection({endPoint}) entered.");
            var clientTcpConnection =
                TcpConnection.CreateConnectingTcpConnection(
                    Guid.NewGuid(),
                    endPoint,
                    new TcpClientConnector(),
                    TimeSpan.FromSeconds(120),
                    conn =>
                    {
                        Log.Debug($"TcpBusClientSide.CreateTcpConnection({endPoint}) successfully constructed TcpConnection.");
                        IsConnected = true;
                        ConfigureTcpListener(conn);
                    },
                    (conn, err) =>
                    {
                        IsConnected = false;
                        HandleError(conn, err);
                    },
                    verbose: true);

            return clientTcpConnection;
        }
        
        private void HandleError(ITcpConnection conn, SocketError err)
        {
            // Assume that any connection error means that the server isn't running, yet. Just wait a second and try again.
            RemoveConnection(conn); //client should only have one connection
            Thread.Sleep(1000);
            Log.Debug($"TcpBusClientSide SocketError = {err} - retrying.");
            AddConnection(CreateTcpConnection(CommandEndpoint));
        }

        private void ConfigureTcpListener(ITcpConnection conn)
        {
            Framer.RegisterMessageArrivedCallback(TcpMessageArrived);
            Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> callback = null;
            callback = (x, data) =>
            {
                try
                {
                    Framer.UnFrameData(x.ConnectionId, data);
                }
                catch (PackageFramingException exc)
                {
                    Log.ErrorException(exc, "LengthPrefixMessageFramer.UnFrameData() threw an exception:");
                    // SendBadRequestAndClose(Guid.Empty, string.Format("Invalid TCP frame received. Error: {0}.", exc.Message));
                    return;
                }
                x.ReceiveAsync(callback);
            };
            conn.ReceiveAsync(callback);
        }

        /// <inheritdoc />
        protected override void AddConnection(ITcpConnection connection)
        {
            if (TcpConnections.Any())
                throw new Exception("Only a single TCP connection is allowed from the client side.");
            base.AddConnection(connection);
        }

        private void TcpMessageArrived(Guid connectionId, ArraySegment<byte> obj)
        {
            try
            {
                var message = DeserializeMessage(obj);
                PublishIncoming(message);
            }
            catch (Exception e)
            {
                Log.ErrorException(e, "An error occurred handling an incoming message over a TCP bus.");
            }
        }

        public override void Handle(IMessage message)
        {
            var type = message.GetType();
            Log.Trace($"Message {message.MsgId} (Type {type.Name}) to be sent over TCP.");

            if (TcpConnections == null)
            {
                Log.Debug($"TCP connection not yet established - Message {message.MsgId} (Type {type.Name}) will be discarded.");
                return;
            }

            try
            {
                if (TcpConnections.IsEmpty())
                {
                    Log.Error("Cannot send a message without a connection.");
                    return;
                }
                var connection = TcpConnections.First();
                if (!MessageSerializers.TryGetValue(type, out var serializer))
                    serializer = new SimpleJsonSerializer();
                var json = serializer.SerializeMessage(message);
                var data = Encoder.ToBytes(json, type);
                var framed = Framer.FrameData(data);
                connection.EnqueueSend(framed);
            }
            catch (Exception ex)
            {
                Log.ErrorException(ex, $"Exception caught while handling Message {message.MsgId} (Type {type.Name})");
            }
        }
    }
}
