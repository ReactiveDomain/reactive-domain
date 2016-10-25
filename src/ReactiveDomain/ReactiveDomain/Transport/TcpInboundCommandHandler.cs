using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using ReactiveDomain.Transport.CommandSocket;
using ReactiveDomain.Transport.Framing;
using ReactiveDomain.Bus;
using NLog;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Transport
{
    /// <summary>
    /// Commands that come off of the TCP/IP connection go through the inboundCommandQueuedHandler,
    /// which then hands them to this class via Handle(message).  This class casts them to
    /// Commands and then fires them on the MessageBus.  When Fire() returns, this class
    /// takes the CommandResponse and publishes it (cast as a Message) on the outboundMessageQueuedHandler.
    /// </summary>
    public class TcpInboundCommandHandler : IHandle<Message>
    {
        private static readonly Logger Log = NLog.LogManager.GetLogger("ReactiveDomain");
        private readonly QueuedHandler _outboundCommandQueuedHandler;
        protected readonly IGeneralBus _mainBus;

        public TcpInboundCommandHandler(IGeneralBus mainBus, QueuedHandler outboundCommandQueuedHandler)
        {
            _outboundCommandQueuedHandler = outboundCommandQueuedHandler;
            _mainBus = mainBus;
        }

        public virtual void PostHandleCommand(Command command, Type type, TimeSpan handleTimeSpan, CommandResponse response)
        {
            var succeeded = response is Success;
            Log.Trace("Command " + type.Name + " MsgId= " + command.MsgId + " Success=" + succeeded + " took " + handleTimeSpan.TotalMilliseconds + "msec to process.");
        }

        public virtual void CommandReceived(dynamic cmd, Type type, string firedBy)
        {
        }

        public void Handle(Message message)
        {
            var type1 = MessageHierarchy.MsgTypeByTypeId[message.MsgTypeId];
            dynamic cmd = message;

            var type2 = cmd.GetType();
            if (!type1.Equals(type2))
            {
                var error =
                    $"Command object-type mismatch.  MsgTypeId={message.MsgTypeId}, which MessageHierarchy claims is a {type1.FullName}.  However, .Net Reflection says that the command is a {type2.FullName}";
                Log.Error(error);
                throw new Exception(error);
            }

            var command = message as Command;
            if (command == null)
            {
                Log.Error("Message " + type1.Name + " MsgId= " + message.MsgId +" received from TCP, but is not a Command");
                return;
            }

            var before = DateTime.UtcNow;
            CommandReceived(cmd, type1, "TcpBusSide");

            CommandResponse response;
            try
            {
                
                _mainBus.TryFire(command, out response);
            }
            catch (Exception ex)
            {
                response = command.Fail(ex);
            }

            _outboundCommandQueuedHandler.Publish(response);
            PostHandleCommand(command, type1, (DateTime.UtcNow - before), response);
        }
    }
}
