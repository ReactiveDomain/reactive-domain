using System;
using System.Collections.Generic;
using System.Net;
using System.Timers;
using ReactiveDomain.Transport.Framing;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Transport.Serialization;
using Microsoft.Extensions.Logging;

namespace ReactiveDomain.Transport
{
    public abstract class TcpBusSide : IHandle<IMessage>
    {
        protected static readonly ILogger Log = Logging.LogProvider.GetLogger("ReactiveDomain");
        protected readonly IDispatcher MessageBus;
        private List<Type> _inboundSpamMessageTypes;
        private QueuedHandlerDiscarding _inboundSpamMessageQueuedHandler;
        private QueuedHandler _inboundMessageQueuedHandler;
        private IMessageSerializer _messageSerializer;
        protected Timer StatsTimer;
        protected List<ITcpConnection> TcpConnection = new List<ITcpConnection>();
        protected LengthPrefixMessageFramer Framer = new LengthPrefixMessageFramer();
        

        protected TcpBusSide(
            IPAddress hostIp,
            int commandPort,
            IDispatcher messageBus,
            IMessageSerializer messageSerializer = null)
            : this(new IPEndPoint(hostIp, commandPort), messageBus, messageSerializer)
        { }

        protected TcpBusSide(
            EndPoint endpoint,
            IDispatcher messageBus,
            IMessageSerializer messageSerializer = null)
        {
            CommandEndpoint = endpoint;
            _messageSerializer = messageSerializer ?? new MessageSerializer();
            MessageBus = messageBus;
            StatsTimer = new Timer(60000);             // getting the stats takes a while - only do it once a minute
            StatsTimer.Elapsed += _statsTimer_Elapsed;
            StatsTimer.Enabled = true;
        }

        public EndPoint CommandEndpoint { get; }

        public List<Type> InboundSpamMessageTypes
        {
            set { _inboundSpamMessageTypes = value; }
        }

        public QueuedHandlerDiscarding InboundSpamMessageQueuedHandler
        {
            set { _inboundSpamMessageQueuedHandler = value; }
        }

        public QueuedHandler InboundMessageQueuedHandler
        {
            set { _inboundMessageQueuedHandler = value; }
        }

        void _statsTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (_inboundSpamMessageQueuedHandler == null)
            {
                Log.LogError("_inboundSpamMessageQueuedHandler is null.");
                return;
            }
            Log.LogDebug(_inboundSpamMessageQueuedHandler.GetStatistics().ToString());

            if (_inboundMessageQueuedHandler == null)
            {
                Log.LogError("_inboundMessageQueuedHandler is null.");
                return;
            }
            Log.LogDebug(_inboundMessageQueuedHandler.GetStatistics().ToString());

        }

        protected void TcpMessageArrived(ArraySegment<byte> obj)
        {
            IMessage message;
            try
            {
                message = _messageSerializer.FromBytes(obj);
            }
            catch (Exception ex)
            {
                Log.LogError(ex, "TcpMessage.FromArraySegment() threw an exception:");
                throw;
            }

            var type = message.GetType();
            Log.LogTrace("Message " + message.MsgId + " (Type " + type.Name + ") received from TCP.");

            if (_inboundSpamMessageTypes.Contains(type))
            {
                if (_inboundSpamMessageQueuedHandler == null)
                {
                    Log.LogError("TCP message (a Message) has arrived, but _inboundSpamMessageQueuedHandler is null.");
                    return;
                }

                _inboundSpamMessageQueuedHandler.Publish(message);
            }
            else
            {
                if (_inboundMessageQueuedHandler == null)
                {
                    Log.LogError("TCP message (a Message) has arrived, but _inboundMessageQueuedHandler is null.");
                    return;
                }

                _inboundMessageQueuedHandler.Publish(message);
            }
        }

        public void Handle(IMessage message)
        {
            var type = message.GetType();
            Log.LogTrace("Message " + message.MsgId + " (Type " + type.Name + ") to be sent over TCP.");

            if (TcpConnection == null)
            {
                Log.LogDebug("TCP connection not yet established - Message " + message.MsgId + " (Type " + type.Name + ") will be discarded.");
                return;
            }

            foreach (var conn in TcpConnection)
            {
                try
                {
                    var data = _messageSerializer.ToBytes(message);
                    var framed = Framer.FrameData(data);
                    conn.EnqueueSend(framed);
                }
                catch (Exception ex)
                {
                    Log.LogError(ex, "Exception caught while handling Message " + message.MsgId + " (Type " + type.Name + ")");
                }
            }
        }
    }

}
