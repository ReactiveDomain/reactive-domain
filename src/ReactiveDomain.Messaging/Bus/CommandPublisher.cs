using System;
using System.Threading.Tasks;

namespace ReactiveDomain.Messaging.Bus
{
    public class CommandPublisher : ICommandPublisher, IDisposable
    {
        private readonly IBus _bus;
        private readonly int _concurrency;
        private readonly CommandManager _manager;
        private readonly TimeSpan? _slowMsgThreshold;
        private readonly TimeSpan? _slowCmdThreshold;
        private readonly MultiQueuedHandler _publisher;
        public bool Idle => _publisher.Idle;
        public CommandPublisher(IBus bus, int concurrency, TimeSpan? slowMsgThreshold, TimeSpan? slowCmdThreshold)
        {
            _bus = bus;
            _concurrency = concurrency;
            _slowMsgThreshold = slowMsgThreshold;
            _slowCmdThreshold = slowCmdThreshold;
            _manager = new CommandManager(bus);
            _publisher = new MultiQueuedHandler(
                _concurrency,
                i => new QueuedHandler(new AdHocHandler<Message>(msg => _bus.Publish(msg))
                , nameof(CommandPublisher)));
            _publisher.Start();
        }
        public void Fire(Command command, string exceptionMsg = null, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null)
        {
            if (command.IsCanceled)
            {
                _publisher.Publish(command.Canceled());
                return;
            }

            var rslt = Execute(command, responseTimeout, ackTimeout);
            if (rslt is Success) return;

            var fail = rslt as Fail;
            if (fail?.Exception != null)
                throw new CommandException(exceptionMsg ?? fail.Exception.Message, fail.Exception, command);
            else
                throw new CommandException(exceptionMsg ?? $"{command.GetType().Name}: Failed", command);
        }
        public bool TryFire(Command command,
            out CommandResponse response,
            TimeSpan? responseTimeout = null,
            TimeSpan? ackTimeout = null)
        {
            try
            {
                //todo: we're not chaining through the fire method here because it doesn't give 
                //us the command response to return so there are some duplicated checks 
                if (command.IsCanceled)
                {
                    response = command.Canceled();
                    _publisher.Publish(response);
                    return false;
                }
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
            return TryFire(command, out var _, responseTimeout, ackTimeout);

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
            try
            {
                //n.b. if this does not throw result will be set asynchronously 
                //in the registered handler in the _manager 
                
                _publisher.Publish(command);
            }
            catch (Exception ex)
            {
                tcs.SetResult(command.Fail(ex));
                throw;
            }
            try
            {
                //blocking caller until result is set 
                return tcs.Task.Result;
            }
            catch (AggregateException aggEx)
            {
                if (aggEx.InnerException != null)
                {
                    throw aggEx.InnerException;
                }
                throw;
            }
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

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _manager?.Dispose();
                _publisher?.Stop();//TODO: do we need to flush/empty the queue here?
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
