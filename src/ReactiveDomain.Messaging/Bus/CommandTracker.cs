using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace ReactiveDomain.Messaging.Bus {
    public class CommandTracker : IDisposable {
        private static readonly ILogger Log = Logging.LogProvider.GetLogger("ReactiveDomain");
        private readonly ICommand _command;
        private readonly TaskCompletionSource<CommandResponse> _tcs;
        private readonly IPublisher _bus;
        private readonly Action _completionAction;
        private readonly Action _cancelAction;
        private bool _disposed;

        private const long PendingAck = 0;
        private const long PendingResponse = 1;
        private const long Complete = 2;
        private long _state;


        public CommandTracker(
            ICommand command,
            TaskCompletionSource<CommandResponse> tcs,
            Action completionAction,
            Action cancelAction,
            TimeSpan ackTimeout,
            TimeSpan completionTimeout,
            IPublisher bus) {

            _command = command;
            _tcs = tcs;
            _bus = bus;
            _completionAction = completionAction;
            _cancelAction = cancelAction;
            _state = PendingAck;
            _bus.Publish(new DelaySendEnvelope(TimeSource.System, ackTimeout, new AckTimeout(_command.MsgId)));
            _bus.Publish(new DelaySendEnvelope(TimeSource.System, completionTimeout, new CompletionTimeout(_command.MsgId)));

        }

        public void Handle(CommandResponse message) {
            Interlocked.Exchange(ref _state, Complete);
            if (_tcs.TrySetResult(message)) _completionAction();
        }

        private long _ackCount;
        public void Handle(AckCommand message) {
            Interlocked.Increment(ref _ackCount);
            var curState = Interlocked.Read(ref _state);
            if (curState != PendingAck || Interlocked.CompareExchange(ref _state, PendingResponse, curState) != curState) {
                //if (Log.LogLevel >= LogLevel.Error)
                //    Log.Error(_command.GetType().Name + " Multiple Handlers Acked Command");
                Log.LogError(_command.GetType().Name + " Multiple Handlers Acked Command");
                if (_tcs.TrySetException(new CommandOversubscribedException(" multiple handlers responded to the command", _command)))
                    _cancelAction();
                return;
            }
         }

        public void Handle(AckTimeout message) {
            if (Interlocked.Read(ref _state) == PendingAck) {
                if (_tcs.TrySetException(new CommandNotHandledException(" timed out waiting for a handler to start. Make sure a command handler is subscribed", _command))) {
                    //if (Log.LogLevel >= LogLevel.Error)
                    //    Log.Error(_command.GetType().Name + " command not handled (no handler)");
                    Log.LogError(_command.GetType().Name + " command not handled (no handler)");
                    _cancelAction();
                }
            }
        }

        public void Handle(CompletionTimeout message) {
            if (Interlocked.Read(ref _state) == PendingResponse) {
                if (_tcs.TrySetException(new CommandTimedOutException(" timed out waiting for handler to complete.", _command))) {
                    //if (Log.LogLevel >= LogLevel.Error)
                    //    Log.Error(_command.GetType().Name + " command timed out");
                    Log.LogError(_command.GetType().Name + " command timed out");
                    _cancelAction();
                }
            }
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);

        }

        public void Dispose(bool disposing) {
            if (_disposed)
                return;

            if (disposing) {
                if (!_tcs.Task.IsCanceled && !_tcs.Task.IsCompleted && !_tcs.Task.IsFaulted) {
                    _tcs.TrySetCanceled();
                }
                _tcs.Task.Dispose();
            }
            _disposed = true;
        }
    }

    public class AckTimeout : IMessage {
        public Guid MsgId { get; private set; }
        public readonly Guid CommandId;
        public AckTimeout(
            Guid commandId) {
            MsgId = Guid.NewGuid();
            CommandId = commandId;
        }
    }

    public class CompletionTimeout : IMessage {
        public Guid MsgId { get; private set; }
        public readonly Guid CommandId;
        public CompletionTimeout(
            Guid commandId) {
            MsgId = Guid.NewGuid();
            CommandId = commandId;
        }
    }
}