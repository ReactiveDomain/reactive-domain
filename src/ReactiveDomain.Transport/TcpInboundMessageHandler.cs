using System;
using ReactiveDomain.Logging;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Transport
{
    /// <summary>
    /// AccountCommands, Events and Messages that come off of the TCP/IP connection go through the 
    /// inboundMessageQueuedHandler, which then hands them to this class via Handle(message).  This 
    /// class simply publishes them on the MessageBus.  HOWEVER, we need to make sure that the
    /// TcpOutboundMessageHandler (which will immediatly get the message from the MessageBus)
    /// doesn't process the message (this would result in a feedback loop, which would be BAD). 
    /// </summary>
    public class TcpInboundMessageHandler : IHandle<Message>
    {
        private static readonly ILogger Log = LogManager.GetLogger("ReactiveDomain");
        protected readonly IDispatcher _mainBus;
        private readonly TcpOutboundMessageHandler _tcpOutboundMessageHandler;
        public TcpInboundMessageHandler(IDispatcher mainBus, TcpOutboundMessageHandler tcpOutboundMessageHandler)
        {
            _mainBus = mainBus;
            _tcpOutboundMessageHandler = tcpOutboundMessageHandler;
        }

        public virtual void PostHandleMessage(dynamic msg, Type type, TimeSpan handleTimeSpan)
        {
            Log.Trace($"Message {type.Name} MsgId= { msg.MsgId } took { handleTimeSpan.TotalMilliseconds } msec to process.");
        }

        public virtual void MessageReceived(dynamic msg, Type type, string publishedBy)
        {
        }


        public void Handle(Message message)
        {
            Type type1 = message.GetType();
            dynamic msg = message;
            var type2 = msg.GetType();
            if (!type1.Equals(type2))
            {
                var error =
                    $"Message object-type mismatch.  MsgType={message.GetType().FullName}, which MessageHierarchy claims is a {type1.FullName}.  However, .Net Reflection says that the command is a {type2.FullName}";
                Log.Error(error);
                throw new Exception(error);
            }

            var before = DateTime.UtcNow;
            MessageReceived(msg, type1, "TcpBusSide");

            _tcpOutboundMessageHandler.IgnoreThisMessage(message);
            Type type = message.GetType();
            _mainBus.Publish(message);
            PostHandleMessage(msg, type1, (DateTime.UtcNow - before));
        }
    }
}
