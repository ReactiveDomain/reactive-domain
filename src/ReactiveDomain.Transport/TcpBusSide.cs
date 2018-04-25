using System;
using System.Collections.Generic;
using System.Net;
using System.Timers;
using ReactiveDomain.Transport.CommandSocket;
using ReactiveDomain.Transport.Framing;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Logging;

namespace ReactiveDomain.Transport
{
    public abstract class TcpBusSide : IHandle<Message>
    {
        protected static readonly ILogger Log = LogManager.GetLogger("ReactiveDomain");
        protected readonly IDispatcher MessageBus;
        private List<Type> _inboundSpamMessageTypes;
        private QueuedHandlerDiscarding _inboundSpamMessageQueuedHandler;
        private QueuedHandler _inboundMessageQueuedHandler;
        protected Timer StatsTimer;
        protected List<ITcpConnection> TcpConnection = new List<ITcpConnection>();
        protected LengthPrefixMessageFramer Framer = new LengthPrefixMessageFramer();

        protected TcpBusSide(
            IPAddress hostIp,
            int commandPort,
            IDispatcher messageBus)
        {
            _hostIp = hostIp;
            _commandPort = commandPort;
            MessageBus = messageBus;
            StatsTimer = new Timer(60000);             // getting the stats takes a while - only do it once a minute
            StatsTimer.Elapsed += _statsTimer_Elapsed;
            StatsTimer.Enabled = true;
        }

        /// <summary>
        /// IP address of the Host application
        /// </summary>        
        private IPAddress _hostIp;
        private int _commandPort;

        public IPEndPoint CommandEndpoint
        {
            get
            {
                if (_commandEndpoint == null)
                    _commandEndpoint = new IPEndPoint(_hostIp, _commandPort);
                return _commandEndpoint;
            }
        }

        private IPEndPoint _commandEndpoint;
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
                Log.Error("_inboundSpamMessageQueuedHandler is null.");
                return;
            }
            Log.Debug(_inboundSpamMessageQueuedHandler.GetStatistics().ToString());

            if (_inboundMessageQueuedHandler == null)
            {
                Log.Error("_inboundMessageQueuedHandler is null.");
                return;
            }
            Log.Debug(_inboundMessageQueuedHandler.GetStatistics().ToString());

        }

        protected void TcpMessageArrived(ArraySegment<byte> obj)
        {
            TcpMessage tcpMessage;
            try
            {
                tcpMessage = TcpMessage.FromArraySegment(obj);
            }
            catch (Exception ex)
            {
                Log.ErrorException(ex, "TcpMessage.FromArraySegment() threw an exception:");
                throw;
            }

            Type type = tcpMessage.WrappedMessage.GetType();
            Log.Trace("Message " + tcpMessage.WrappedMessage.MsgId + " (Type " + type.Name + ") received from TCP.");

            if (_inboundSpamMessageTypes.Contains(type))
            {
                if (_inboundSpamMessageQueuedHandler == null)
                {
                    Log.Error("TCP message (a Message) has arrived, but _inboundSpamMessageQueuedHandler is null.");
                    return;
                }

                _inboundSpamMessageQueuedHandler.Publish(tcpMessage.WrappedMessage);
            }
            else
            {
                if (_inboundMessageQueuedHandler == null)
                {
                    Log.Error("TCP message (a Message) has arrived, but _inboundMessageQueuedHandler is null.");
                    return;
                }

                _inboundMessageQueuedHandler.Publish(tcpMessage.WrappedMessage);
            }
        }

        public void Handle(Message message)
        {
            Type type = message.GetType();
            Log.Trace("Message " + message.MsgId + " (Type " + type.Name + ") to be sent over TCP.");

            if (TcpConnection == null)
            {
                Log.Debug("TCP connection not yet established - Message " + message.MsgId + " (Type " + type.Name + ") will be discarded.");
                return;
            }

            foreach (var conn in TcpConnection)
            {
                try
                {
                    var framed = Framer.FrameData(new TcpMessage(message).Data);
                    conn.EnqueueSend(framed);
                }
                catch (Exception ex)
                {
                    Log.ErrorException(ex, "Exception caught while handling Message " + message.MsgId + " (Type " + type.Name + ")");
                }
            }
        }
    }

}
