using System;
using System.Threading;
using System.Threading.Tasks;
using ReactiveDomain.Messaging.Logging;

namespace ReactiveDomain.Messaging.Bus
{
    public class CommandTracker : IDisposable

    {
        private static readonly ILogger Log = LogManager.GetLogger("ReactiveDomain");
        private readonly Command _command;
        private readonly TaskCompletionSource<CommandResponse> _tcs;
        private readonly TimeSpan _completionTimeout;
        private readonly Action _completionAction;
        private readonly Action _cancelAction;
        private bool _disposed = false;

        private const long PendingAck = 0;
        private const long PendingResponse = 1;
        private const long Complete = 2;
        private long _state;
        private Timer _ackTimer;
        private Timer _completionTimer;

        public CommandTracker(
            Command command,
            TaskCompletionSource<CommandResponse> tcs,
            Action completionAction,
            Action cancelAction,
            TimeSpan ackTimeout,
            TimeSpan completionTimeout)
        {
            _command = command;
            _tcs = tcs;
            _completionTimeout = completionTimeout;
            _completionAction = completionAction;
            _cancelAction = cancelAction;
            _state = PendingAck;
            _ackTimer = new Timer(AckTimeout, null, (int)ackTimeout.TotalMilliseconds, Timeout.Infinite);
            
        }

        public void Handle(CommandResponse message)
        {
            Interlocked.Exchange(ref _state, Complete);
            if (_tcs.TrySetResult(message)) _completionAction();
        }

        private long _ackCount = 0;
        public void Handle(AckCommand message)
        {
            Interlocked.Increment(ref _ackCount);
            var curState = Interlocked.Read(ref _state);
            if (curState != PendingAck || Interlocked.CompareExchange(ref _state, PendingResponse, curState) != curState)
            {
                if (Log.LogLevel >= LogLevel.Error)
                    Log.Error(_command.GetType().Name + " Multiple Handlers Acked Command");
                if (_tcs.TrySetException(new CommandOversubscribedException(" multiple handlers responded to the command", _command)))
                    _cancelAction();
                return;
            }
            _completionTimer = new Timer(CompletionTimeout, null, (int)_completionTimeout.TotalMilliseconds, Timeout.Infinite);

        }

        public void AckTimeout(object state = null)
        {
            if (Interlocked.Read(ref _state) == PendingAck)
            {
                if (_tcs.TrySetException(new CommandNotHandledException(" timed out waiting for handler to start.", _command)))
                {
                    if (Log.LogLevel >= LogLevel.Error)
                        Log.Error(_command.GetType().Name + " command not handled (no handler)");
                    _cancelAction();
                }
            }            
        }

        public void CompletionTimeout(object state = null)
        {
            if (Interlocked.Read(ref _state) == PendingResponse)
            {
                if (_tcs.TrySetException(new CommandTimedOutException(" timed out waiting for handler to complete.", _command)))
                {
                    if (Log.LogLevel >= LogLevel.Error)
                        Log.Error(_command.GetType().Name + " command timed out");
                    _cancelAction();
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);

        }

        public void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                if (!_tcs.Task.IsCanceled && !_tcs.Task.IsCompleted && !_tcs.Task.IsFaulted)
                {
                    _tcs.TrySetCanceled();
                }
                _tcs.Task.Dispose();
                _ackTimer?.Dispose();
                _completionTimer?.Dispose();

            }
            _disposed = true;
        }
    }
}