using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using ReactiveDomain.Messaging;
using ReactiveDomain.Transport.Framing;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Transport.Serialization;
using ReactiveDomain.Util;

namespace ReactiveDomain.Transport
{
    public sealed class TcpBusServerSide : TcpBus
    {
        private readonly TcpServerListener _commandPortListener;
        /// <summary>Routing ID, keyed by MsgId. Assumes that the only outgoing messages are CommandResponses.</summary>
        private readonly ConcurrentDictionary<Guid, Guid> _messageRouting = new ConcurrentDictionary<Guid, Guid>();

        public TcpBusServerSide(
            IPAddress hostIp,
            int commandPort,
            IEnumerable<Type> inboundDiscardingMessageTypes = null,
            QueuedHandlerDiscarding inboundDiscardingMessageQueuedHandler = null,
            IEnumerable<Type> inboundNondiscardingMessageTypes = null,
            QueuedHandler inboundNondiscardingMessageQueuedHandler = null,
            Dictionary<Type, IMessageSerializer> messageSerializers = null)
            : base(
                hostIp,
                commandPort,
                inboundDiscardingMessageTypes,
                inboundDiscardingMessageQueuedHandler,
                inboundNondiscardingMessageTypes,
                inboundNondiscardingMessageQueuedHandler,
                messageSerializers)
        {
            Log.Debug($"Configuring TCP Listener at {CommandEndpoint.AddressFamily}, {CommandEndpoint}.");

            var listener = new TcpServerListener(CommandEndpoint);

            listener.StartListening(
                (endPoint, socket) =>
                {
                    var connectionId = Guid.NewGuid();
                    var conn = TcpConnection.CreateAcceptedTcpConnection(connectionId, endPoint, socket, verbose: true);

                    var framer = new LengthPrefixMessageFramer();
                    framer.RegisterMessageArrivedCallback(TcpMessageArrived);

                    Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> callback = null;
                    callback = (x, data) =>
                    {
                        try
                        {
                            framer.UnFrameData(x.ConnectionId, data);
                        }
                        catch (PackageFramingException exc)
                        {
                            Log.ErrorException(exc, "LengthPrefixMessageFramer.UnFrameData() threw an exception:");
                            return;
                        }
                        x.ReceiveAsync(callback);
                    };
                    conn.ReceiveAsync(callback);
                    AddConnection(conn);
                },
                "Standard");
            Log.Debug($"TCP Listener at {CommandEndpoint.AddressFamily}, {CommandEndpoint} successfully configured.");
            _commandPortListener = listener;
        }

        private void TcpMessageArrived(Guid connectionId, ArraySegment<byte> obj)
        {
            try
            {
                var message = DeserializeMessage(obj);
                _messageRouting[message.MsgId] = connectionId;
                PublishIncoming(message);
            }
            catch (Exception e)
            {
                Log.ErrorException(e, "An error occurred handling an incoming message over a TCP bus.");
            }
        }

        public override void Handle(IMessage message)
        {
            // The server side does not initiate communication. The only messages it will ever send are
            // CommandResponses back to a client who sent a Command.
            var type = message.GetType();
            if (!(message is CommandResponse cmdResponse))
            {
                Log.Debug($"Cannot send a message of type {type.Name} from a server-side TCP bus.");
                return;
            }
            Log.Trace($"Message {message.MsgId} (Type {type.Name}) to be sent over TCP.");

            if (TcpConnections.IsEmpty())
            {
                Log.Debug($"TCP connection not yet established - Message {message.MsgId} (Type {type.Name}) will be discarded.");
                return;
            }

            try
            {
                // Send the CommandResponse back to the endpoint where the Command originated.
                var connectionId = _messageRouting[cmdResponse.SourceCommand.MsgId];
                var connection = TcpConnections.FirstOrDefault(x => x.ConnectionId == connectionId);
                if (connection is null)
                    throw new Exception("Could not find a TCP connection to use for sending the message.");

                if (!MessageSerializers.TryGetValue(type, out var serializer))
                    serializer = new SimpleJsonSerializer();
                var json = serializer.SerializeMessage(message);
                var data = Encoder.ToBytes(json, type);
                var framed = Framer.FrameData(data);
                connection.EnqueueSend(framed);
            }
            catch (Exception ex)
            {
                Log.ErrorException(ex, $"An error occurred while handling Message {message.MsgId} (Type {type.Name})");
            }
        }

        private bool _disposed;
        protected override void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                _commandPortListener.Stop();
            }
            _disposed = true;
            base.Dispose(disposing);
        }
    }
}
