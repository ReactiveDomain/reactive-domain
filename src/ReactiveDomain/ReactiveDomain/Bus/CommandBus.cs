using System;
using System.Collections.Generic;
using System.Reactive;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using NLog;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Bus
{

    public class CommandBus : InMemoryBus, IGeneralBus
    {
        private readonly TimeSpan _slowMsgThreshold;
        private readonly TimeSpan _slowCmdThreshold;
        private static readonly Logger Log = NLog.LogManager.GetLogger("ReactiveDomain");
        private readonly CommandManager _manager;
        private readonly Dictionary<Type, object> _handleWrappers;

        public CommandBus(
                    string name,
                    bool watchSlowMsg = true,
                    TimeSpan? slowMsgThreshold = null,
                    TimeSpan? slowCmdThreshold = null)
            : base(name, watchSlowMsg, slowMsgThreshold)
        {
            _slowMsgThreshold = slowMsgThreshold ?? TimeSpan.FromMilliseconds(100);
            _slowCmdThreshold = slowCmdThreshold ?? TimeSpan.FromMilliseconds(500);
            _manager = new CommandManager(this);
            _handleWrappers = new Dictionary<Type, object>();
        }

        public void RequestCancel(Command command)
        {
            try
            {
                Publish(command.BuildCancel());
            }
            catch
            {
                //ignore
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="command"></param>
        /// <param name="exceptionMsg"></param>
        /// <param name="responseTimeout"></param>
        /// <param name="ackTimeout"></param>
        /// <returns></returns>
        public void Fire(
                        Command command,
                        string exceptionMsg = null,
                        TimeSpan? responseTimeout = null,
                        TimeSpan? ackTimeout = null)
        {
            var rslt = Execute(command, responseTimeout, ackTimeout);
            if (rslt is Success) return;

            var fail = rslt as Fail;
            if (fail?.Exception != null)
                throw new CommandException(exceptionMsg ?? fail.Exception.Message, fail.Exception, command);
            else
                throw new CommandException(exceptionMsg ?? $"{command.GetType().Name}: Failed", command);
        }
        private CommandResponse Execute(
                                    Command command,
                                    TimeSpan? responseTimeout = null,
                                    TimeSpan? ackTimeout = null)
        {
            TaskCompletionSource<CommandResponse> tcs = null;
            try
            {
                CommandReceived(command, command.GetType(), "");
                tcs = _manager.RegisterCommandAsync(
                    command,
                    ackTimeout ?? _slowMsgThreshold,
                    responseTimeout ?? _slowCmdThreshold);
            }
            catch (CommandException ex)
            {
                tcs?.SetResult(command.Fail(ex));
                throw;
            }
            catch (Exception ex)
            {
                tcs?.SetResult(command.Fail(ex));
                throw new CommandException("Error executing command: ", ex, command);
            }


            Task.Run(() =>
            {
                try
                {
                    //n.b. if this does not throw result will be set asynchronously 
                    //in the registered handler in the _manager 
                    Publish(command);
                }
                catch (Exception ex)
                {
                    tcs.SetResult(command.Fail(ex));
                    throw;
                }
            });
            try
            {
                return tcs.Task.Result;
            }
            catch (AggregateException aggEx)
            {
                throw aggEx.InnerException;
            }
        }

        public bool TryFire(Command command,
                        out CommandResponse response,
                        TimeSpan? responseTimeout = null,
                        TimeSpan? ackTimeout = null)
        {
            try
            {
                if (!HasSubscriberFor(command.MsgTypeId))
                   // Publish(command); //a connected bus might handle this
                    throw new CommandNotHandledException(command);
                response = Execute(command, responseTimeout, ackTimeout);
            }
            catch (Exception ex)
            {
                response = command.Fail(ex);
            }
            return response is Success;
        }

        public bool TryFire(Command command, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null)
        {
            CommandResponse resp;
            return TryFire(command, out resp, responseTimeout, ackTimeout);
        }

        public IDisposable Subscribe<T>(IHandleCommand<T> handler) where T : Command
        {
            if (HasSubscriberFor<T>())
                throw new ExistingHandlerException("Duplicate registration for command type.");
            var handleWrapper = new CommandHandler<T>(this, handler);
            _handleWrappers.Add(typeof(T), handleWrapper);
            Subscribe(handleWrapper);
            Subscribe(handleWrapper.CancelHandler);
            return new SubscriptionDisposer(() => { this?.Unsubscribe(handler); return Unit.Default; });
        }
        public void Unsubscribe<T>(IHandleCommand<T> handler) where T : Command
        {
            object wrapper;
            if (!_handleWrappers.TryGetValue(typeof(T), out wrapper)) return;
            Unsubscribe((CommandHandler<T>)wrapper);
            Unsubscribe(((CommandHandler<T>)wrapper).CancelHandler);
            _handleWrappers.Remove(typeof(T));
        }

        public virtual void NoCommandHandler(dynamic cmd, Type type)
        {
            //replace with message published on ack timeout
            //We can't know this here
        }

        public virtual void PostHandleCommand(dynamic cmd, Type type, string handlerName, dynamic response,
            TimeSpan handleTimeSpan)
        {
            //replace with message published from Command handler
            //We can't know this here
        }

        public virtual void CommandReceived(dynamic cmd, Type type, string firedBy)
        {
            //replace with message published from Command handler
            //We can't know this here
        }
    }


    [Serializable]
    public class CommandException : Exception
    {
        private readonly Command _command;

        public CommandException(Command command) : base($"{command?.GetType().Name}: Failed")
        {
            _command = command;
        }

        public CommandException(string message, Command command) : base($"{command?.GetType().Name}: {message}")
        {
            _command = command;
        }

        public CommandException(string message, Exception inner, Command command) : base($"{command?.GetType().Name}: {message}", inner)
        {
            _command = command;
        }

        protected CommandException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }

    [Serializable]
    public class CommandNotHandledException : CommandException
    {

        public CommandNotHandledException(Command command) : base(" not handled", command)
        {
        }

        public CommandNotHandledException(string message, Command command) : base(message, command)
        {
        }

        public CommandNotHandledException(string message, Exception inner, Command command) : base(message, inner, command)
        {
        }

        protected CommandNotHandledException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
    [Serializable]
    public class CommandCanceledException : CommandException
    {

        public CommandCanceledException(Command command) : base(" canceled", command)
        {
        }

        public CommandCanceledException(string message, Command command) : base(message, command)
        {
        }

        public CommandCanceledException(string message, Exception inner, Command command) : base(message, inner, command)
        {
        }

        protected CommandCanceledException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
    [Serializable]
    public class CommandTimedOutException : CommandException
    {

        public CommandTimedOutException(Command command) : base(" timed out", command)
        {
        }

        public CommandTimedOutException(string message, Command command) : base(message, command)
        {
        }

        public CommandTimedOutException(string message, Exception inner, Command command) : base(message, inner, command)
        {
        }

        protected CommandTimedOutException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
    /// <summary>
    /// More than one handler acked this message.
    /// Most of this means the command is subscribed on
    /// multiple connected buses
    /// </summary>
    [Serializable]
    public class CommandOversubscribedException : CommandException
    {

        public CommandOversubscribedException(Command command) : base(" oversubscribed", command)
        {
        }

        public CommandOversubscribedException(string message, Command command) : base(message, command)
        {
        }

        public CommandOversubscribedException(string message, Exception inner, Command command) : base(message, inner, command)
        {
        }

        protected CommandOversubscribedException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }
    [Serializable]
    public class ExistingHandlerException : Exception
    {

        public ExistingHandlerException()
        {
        }

        public ExistingHandlerException(string message) : base(message)
        {
        }

        public ExistingHandlerException(string message, Exception inner) : base(message, inner)
        {
        }

        protected ExistingHandlerException(
            SerializationInfo info,
            StreamingContext context) : base(info, context)
        {
        }
    }

}
