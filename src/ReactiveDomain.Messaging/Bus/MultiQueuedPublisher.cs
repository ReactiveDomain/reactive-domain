using System;
using System.Threading.Tasks;

namespace ReactiveDomain.Messaging.Bus {
    public class MultiQueuedPublisher : ICommandPublisher, IPublisher, IDisposable {
        private readonly CommandManager _manager;
        private readonly TimeSpan? _slowMsgThreshold;
        private readonly TimeSpan? _slowCmdThreshold;
        private readonly MultiQueuedHandler _publishQueue;
        private readonly LaterService _laterService;
        private readonly InMemoryBus _timeoutBus;
        public bool Idle => _publishQueue.Idle;
        public MultiQueuedPublisher(
                IBus bus,
                int queueCount,
                TimeSpan? slowMsgThreshold,
                TimeSpan? slowCmdThreshold) {
            _slowMsgThreshold = slowMsgThreshold;
            _slowCmdThreshold = slowCmdThreshold;
            _timeoutBus = new InMemoryBus(nameof(_timeoutBus),false);
            _laterService = new LaterService(_timeoutBus, TimeSource.System);
            _timeoutBus.Subscribe<DelaySendEnvelope>(_laterService);
            _laterService.Start();

            _manager = new CommandManager(bus,_timeoutBus);
            _timeoutBus.Subscribe<AckTimeout>(_manager);
            _timeoutBus.Subscribe<CompletionTimeout>(_manager);

            _publishQueue = new MultiQueuedHandler(
                queueCount,
                i => new QueuedHandler(new AdHocHandler<Message>(bus.Publish)
                , nameof(MultiQueuedPublisher)));
            _publishQueue.Start();
        }
        public void Publish(Message message) {
            _publishQueue.Publish(message);
        }
        public void Send(Command command, string exceptionMsg = null, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null) {
            if (command.IsCanceled) {
                _publishQueue.Publish(command.Canceled());
                throw new CommandCanceledException(command);
            }

            Execute(command, out var rslt, true, responseTimeout, ackTimeout);
            if (rslt is Success) return;

            var fail = rslt as Fail;
            if (fail?.Exception != null)
                throw new CommandException(exceptionMsg ?? fail.Exception.Message, fail.Exception, command);
            else
                throw new CommandException(exceptionMsg ?? $"{command.GetType().Name}: Failed", command);
        }
        public bool TrySend(Command command,
            out CommandResponse response,
            TimeSpan? responseTimeout = null,
            TimeSpan? ackTimeout = null) {
            try {
                if (command.IsCanceled) {
                    response = command.Canceled();
                    _publishQueue.Publish(response);
                    return false;
                }
                Execute(command, out response, true, responseTimeout, ackTimeout);
            }
            catch (Exception ex) {
                response = command.Fail(ex);
            }
            return response is Success;
        }

        public bool TrySendAsync(Command command, TimeSpan? responseTimeout = null, TimeSpan? ackTimeout = null) {
            try {
                if (command.IsCanceled) {
                    var response = command.Canceled();
                    _publishQueue.Publish(response);
                    return false;
                }
                Execute(command, out var _, false, responseTimeout, ackTimeout);
            }
            catch (Exception) {
                return false;
            }
            return true;

        }

        private void Execute(
            Command command,
            out CommandResponse response,
            bool blocking = true,
            TimeSpan? responseTimeout = null,
            TimeSpan? ackTimeout = null) {

            TaskCompletionSource<CommandResponse> tcs = null;
            try {
                tcs = _manager.RegisterCommandAsync(
                    command,
                    ackTimeout ?? _slowMsgThreshold,
                    responseTimeout ?? _slowCmdThreshold);
            }
            catch (CommandException ex) {
                tcs?.SetResult(command.Fail(ex));
                throw;
            }
            catch (Exception ex) {
                tcs?.SetResult(command.Fail(ex));
                throw new CommandException("Error executing command: ", ex, command);
            }
            try {
                //n.b. if this does not throw result will be set asynchronously 
                //in the registered handler in the _manager 

                _publishQueue.Publish(command);
            }
            catch (Exception ex) {
                tcs.SetResult(command.Fail(ex));
                throw;
            }

            if (!blocking) {
                response = null;
                return;
            }
            try {
                //blocking caller until result is set 
                response = tcs.Task.Result;
            }
            catch (AggregateException aggEx) {
                if (aggEx.InnerException != null) {
                    throw aggEx.InnerException;
                }
                throw;
            }
        }

        protected virtual void Dispose(bool disposing) {
            if (disposing) {
                _laterService?.Dispose();
                _manager?.Dispose();
                _timeoutBus.Dispose();
                _publishQueue?.Stop();//TODO: do we need to flush/empty the queue here?
            }
        }
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
