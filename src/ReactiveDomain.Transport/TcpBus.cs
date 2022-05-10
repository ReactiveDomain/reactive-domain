using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Timers;
using ReactiveDomain.Logging;
using ReactiveDomain.Transport.Framing;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Transport.Serialization;

namespace ReactiveDomain.Transport
{
    public abstract class TcpBus : IHandle<IMessage>, IDisposable
    {
        protected static readonly ILogger Log = LogManager.GetLogger("ReactiveDomain");
        private readonly IEnumerable<Type> _inboundDiscardingMessageTypes;
        private readonly QueuedHandlerDiscarding _inboundDiscardingMessageQueuedHandler;
        private readonly IEnumerable<Type> _inboundNondiscardingMessageTypes;
        private readonly QueuedHandler _inboundNondiscardingMessageQueuedHandler;
        protected readonly Dictionary<Type, IMessageSerializer> MessageSerializers;
        protected readonly TcpMessageEncoder Encoder;
        private readonly Timer _statsTimer;
        protected IEnumerable<ITcpConnection> TcpConnections => _tcpConnections;
        private readonly List<ITcpConnection> _tcpConnections = new List<ITcpConnection>();
        protected readonly LengthPrefixMessageFramer Framer = new LengthPrefixMessageFramer();

        protected TcpBus(
            IPAddress hostIp,
            int commandPort,
            IEnumerable<Type> inboundDiscardingMessageTypes = null,
            QueuedHandlerDiscarding inboundDiscardingMessageQueuedHandler = null,
            IEnumerable<Type> inboundNondiscardingMessageTypes = null,
            QueuedHandler inboundNondiscardingMessageQueuedHandler = null,
            Dictionary<Type, IMessageSerializer> messageSerializers = null)
            : this(
                new IPEndPoint(hostIp, commandPort),
                inboundDiscardingMessageTypes,
                inboundDiscardingMessageQueuedHandler,
                inboundNondiscardingMessageTypes,
                inboundNondiscardingMessageQueuedHandler,
                messageSerializers)
        { }

        protected TcpBus(
            EndPoint endpoint,
            IEnumerable<Type> inboundDiscardingMessageTypes = null,
            QueuedHandlerDiscarding inboundDiscardingMessageQueuedHandler = null,
            IEnumerable<Type> inboundNondiscardingMessageTypes = null,
            QueuedHandler inboundNondiscardingMessageQueuedHandler = null,
            Dictionary<Type, IMessageSerializer> messageSerializers = null)
        {
            CommandEndpoint = endpoint;
            _inboundDiscardingMessageTypes = inboundDiscardingMessageTypes?.ToArray() ?? new Type[0];
            _inboundDiscardingMessageQueuedHandler = inboundDiscardingMessageQueuedHandler;
            _inboundNondiscardingMessageTypes = inboundNondiscardingMessageTypes?.ToArray() ?? new Type[0];
            _inboundNondiscardingMessageQueuedHandler = inboundNondiscardingMessageQueuedHandler;
            MessageSerializers = messageSerializers ?? new Dictionary<Type, IMessageSerializer>();
            Encoder = new TcpMessageEncoder();
            _statsTimer = new Timer(60000);             // getting the stats takes a while - only do it once a minute
            _statsTimer.Elapsed += StatsTimer_Elapsed;
            _statsTimer.Enabled = true;
        }

        protected EndPoint CommandEndpoint { get; }

        protected virtual void AddConnection(ITcpConnection connection)
        {
            _tcpConnections.Add(connection);
        }

        protected void RemoveConnection(ITcpConnection connection)
        {
            try
            {
                if (!TcpConnections.Contains(connection))
                    return;
                if (!connection.IsClosed)
                    connection.Close("Connection removed.");
                _tcpConnections.Remove(connection);
            }
            catch (Exception e)
            {
                Log.ErrorException(e, "Error while removing a TCP connection.");
            }
        }

        private void StatsTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (_inboundDiscardingMessageQueuedHandler != null)
            {
                Log.Debug(_inboundDiscardingMessageQueuedHandler.GetStatistics().ToString());
            }
            if (_inboundNondiscardingMessageQueuedHandler != null)
            {
                Log.Debug(_inboundNondiscardingMessageQueuedHandler.GetStatistics().ToString());
            }
        }

        protected IMessage DeserializeMessage(ArraySegment<byte> obj)
        {
            try
            {
                var encodedData = Encoder.FromBytes(obj);
                if (!MessageSerializers.TryGetValue(encodedData.Item2, out var serializer))
                    serializer = new SimpleJsonSerializer();
                return serializer.DeserializeMessage(encodedData.Item1, encodedData.Item2);
            }
            catch (Exception ex)
            {
                Log.ErrorException(ex, "TcpMessage.FromArraySegment() threw an exception:");
                throw;
            }
        }

        protected void PublishIncoming(IMessage message)
        {
            var type = message.GetType();
            Log.Trace($"Message {message.MsgId} (Type {type.Name}) received from TCP.");
            if (_inboundDiscardingMessageTypes.Any(x => x.IsAssignableFrom(type)))
            {
                _inboundDiscardingMessageQueuedHandler?.Publish(message);
            }
            else if (_inboundNondiscardingMessageTypes.Any(x => x.IsAssignableFrom(type)))
            {
                _inboundNondiscardingMessageQueuedHandler?.Publish(message);
            }

        }

        public abstract void Handle(IMessage message);

        private bool _disposed;
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                _statsTimer.Dispose();
                foreach (var connection in TcpConnections)
                {
                    connection.Close("disposing");
                }
                _tcpConnections.Clear();
            }
            _disposed = true;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
