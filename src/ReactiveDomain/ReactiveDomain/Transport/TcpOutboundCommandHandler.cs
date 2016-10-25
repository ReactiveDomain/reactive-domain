using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Permissions;
using System.Threading;
using ReactiveDomain.Transport.CommandSocket;
using ReactiveDomain.Transport.Framing;
using ReactiveDomain.Bus;
using NLog;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Transport
{
    public class TcpOutboundCommandHandler : IHandleCommand<Command>, IHandle<Message>
    {
        protected static readonly Logger Log = NLog.LogManager.GetLogger("ReactiveDomain");
        private readonly IGeneralBus _messageBus;
        private readonly QueuedHandler _outboundCommandQueuedHandler;
        private readonly int _commandTimeout;
        private readonly ConcurrentDictionary<Guid, CommandResponse> _activeCommands = new ConcurrentDictionary<Guid, CommandResponse>();

        public TcpOutboundCommandHandler(IGeneralBus messageBus, QueuedHandler outboundCommandQueuedHandler, int commandTimeout)
        {
            _messageBus = messageBus;
            _commandTimeout = commandTimeout;
            _outboundCommandQueuedHandler = outboundCommandQueuedHandler;
        }


        public void RequestCancel(CancelCommand cancelRequest)
        {
            //TODO: implement cancel if needed
        }

        public CommandResponse Handle(Command command)
        {
            try
            {
                CommandResponse response;
                _activeCommands[command.MsgId] = null;

                Type type = MessageHierarchy.MsgTypeByTypeId[command.MsgTypeId];
                Log.Trace("Command Type=" + type.Name + " MsgId=" + command.MsgId + " to be sent over TCP.");

                _outboundCommandQueuedHandler.Publish(command);

                // I block here until the CommandResponse message is received on the
                // connection, or until I time out.   
                var startWaitTime = DateTime.UtcNow;
                while (_activeCommands[command.MsgId] == null)
                {
                    Thread.Sleep(10);

                    if (command.TimeoutTcpWait)
                    {
                        var waitTimeSpan = DateTime.UtcNow - startWaitTime;
                        if (waitTimeSpan > TimeSpan.FromMilliseconds(_commandTimeout))
                        {
                            throw new Exception("Command response not received within " + _commandTimeout + " milliseconds.");
                        }
                    }
                }
                _activeCommands.TryRemove(command.MsgId, out response);
                var succeeded = response is Success;
                Log.Trace("Response for Command " + type.Name + " MsgId=" + command.MsgId + " Success=" + succeeded + " returned to the Firer.");

                return response;
            }
            catch (Exception ex)
            {
                Log.Error(ex,"Exception thrown attempting to handle command");
                return command.Fail(ex);
            }
        }
        public void Handle(Message message)
        {
            CommandResponse response =  message as CommandResponse;
            Type type = MessageHierarchy.MsgTypeByTypeId[message.MsgTypeId];
            if (response == null)
            {
                Log.Error("Message " + type.Name + " MsgId=" + message.MsgId + " received, but is not a CommandResponse - it will be ignored.");
            }
            else
            {
                type = response.CommandType;
                Log.Trace("Response for Command " + type.Name + " MsgId=" + response.CommandId + " received.");
                _activeCommands[response.MsgId] = response;
            }
        }

    }

}
