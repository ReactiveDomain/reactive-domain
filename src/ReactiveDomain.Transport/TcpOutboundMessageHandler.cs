using System;
using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Transport
{
    public class TcpOutboundMessageHandler : IHandle<IMessage>
    {
        private static readonly ILogger Log = Logging.LogProvider.GetLogger("ReactiveDomain");
        private readonly IDispatcher _messageBus;
        private readonly QueuedHandler _outboundMessageQueuedHandler;

        private readonly ConcurrentDictionary<Guid, IMessage> _messagesThatCameFromTcp =
            new ConcurrentDictionary<Guid, IMessage>();

        public TcpOutboundMessageHandler(
            IDispatcher messageBus,
            QueuedHandler outboundMessageQueuedHandler)
        {
            _messageBus = messageBus;
            _outboundMessageQueuedHandler = outboundMessageQueuedHandler;
            _messageBus.Subscribe(this);
        }

        // Every wrapped message that came out of TCP/IP, TcpInboundMessageHandler published on the bus.  
        // I handle EVERYTHING that comes from the bus, so I'm going to get that same message back 
        // (instantaneously).  Thus TcpInboundMessageHandler calls the IgnoreThisMessage() function,
        // which puts the message on the _messagesThatCameFromTcp list.  TcpInboundMessageHandler then
        // publishes the message on the CommandBus, which immediately hands it back to me.  The Handle()
        // method (below) then keeps me from sending it BACK out on TCP, and thus we avoid the feedback loop.

        public void IgnoreThisMessage(IMessage message)
        {
            Type type = message.GetType();
            if (message.MsgId == Guid.Empty)
            {
                Log.LogError("Message " + message.MsgId + " (Type " + type.Name + ") - INTERNAL ERROR: there should NEVER be a message with that MsgId value");
            }
            if (_messagesThatCameFromTcp.TryAdd(message.MsgId, message))
            {
                Log.LogTrace("Message " + message.MsgId + " (Type " + type.Name +
                          ") came from TCP, now added to MessagesThatCameFromTcp hash.  Hash now contains " +
                          _messagesThatCameFromTcp.Count + " entries.");
            }
        }

        public void Handle(IMessage message)
        {
            Type type = message.GetType();
            IMessage removedMessage;
            if (_messagesThatCameFromTcp.TryRemove(message.MsgId, out removedMessage))
            {
                Log.LogTrace("Message " + message.MsgId + " (Type " + type.Name +
                          ") came from TCP originally, NOT sent back on TCP.  MessagesThatCameFromTcp hash now contains " +
                          _messagesThatCameFromTcp.Count + " entries.");
                return;
            }

            Log.LogTrace("Message " + message.MsgId + " (Type " + type.Name + ") to be sent over TCP.");
            _outboundMessageQueuedHandler.Publish(message);
        }

    }

}
