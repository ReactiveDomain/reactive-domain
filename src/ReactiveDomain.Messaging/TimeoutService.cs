using System;
using System.Threading;

using Microsoft.Extensions.Logging;

using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Util;

namespace ReactiveDomain.Messaging
{
    public class TimeoutMessage : ICorrelatedMessage
    {
        public Guid MsgId { get; private set; }
        public Guid CorrelationId { get; set; }
        public Guid CausationId { get; set; }

        public Guid TargetId { get; private set; }

        /// <summary>
        /// Set this by: TimeoutService.EpochMsFromDateTime(DateTime.UtcNow) + timeout-interval-in-milliseconds
        /// </summary>
        public long TimeoutMs { get; private set; }

        public TimeoutMessage(
                        Guid targetId,
                        long timeoutMs,
                        ICorrelatedMessage source)
        {
            MsgId = Guid.NewGuid();
            TargetId = targetId;
            TimeoutMs = timeoutMs;
            CorrelationId = source.CorrelationId;
            CausationId = source.MsgId;
        }
    }

    public class TimeoutRequestNode : PriorityQueueNode
    {
        public TimeoutRequestNode(
                ICorrelatedMessage source,
                Guid targetId,
                long timeoutMs,
                Action<TimeoutMessage> timeoutAction
            ) : base(timeoutMs)
        {
            Source = source;
            TargetId = targetId;
            TimeoutMs = timeoutMs;
            TimeoutAction = timeoutAction;
        }
        public ICorrelatedMessage Source { get; }
        public Guid TargetId { get; }
        public long TimeoutMs { get; }
        public Action<TimeoutMessage> TimeoutAction;

        public void SendTimeout()
        {
            TimeoutAction(new TimeoutMessage(
                                        TargetId,
                                        TimeoutMs,
                                        Source));
        }
    }

    public class TimeoutService
    {
        private static readonly ILogger Log = Logging.LogProvider.GetLogger("ReactiveDomain");
        private static int size = 5000;
        private readonly object _queueLock = new object();
        private readonly HeapPriorityQueue<TimeoutRequestNode> _queue = new HeapPriorityQueue<TimeoutRequestNode>(size);
        private readonly ManualResetEventSlim _msgAddEvent = new ManualResetEventSlim(false);

        private Thread _thread;
        private volatile bool _stop;
        private volatile bool _starving;
        private readonly ManualResetEventSlim _stopped = new ManualResetEventSlim(true);
        private readonly TimeSpan _threadStopWaitTimeout;



        public TimeoutService()
        {

            _threadStopWaitTimeout = QueuedHandler.DefaultStopWaitTimeout;
        }

        public void Start()
        {
            if (_thread != null)
                throw new InvalidOperationException("Already a thread running.");

            _stopped.Reset();

            _thread = new Thread(ReadFromQueue) { IsBackground = true, Name = "TimeoutService" };
            _thread.Start();
        }

        public void Stop()
        {
            _stop = true;
            if (!_stopped.Wait(_threadStopWaitTimeout))
                throw new TimeoutException("Unable to stop thread TimeoutService");
        }

        public void RequestStop()
        {
            _stop = true;
        }
        public static long EpochMsFromDateTime(DateTime date)
        {
            long unixTimestamp = date.Ticks - new DateTime(1970, 1, 1).Ticks;
            unixTimestamp /= TimeSpan.TicksPerMillisecond;
            return unixTimestamp;
        }

        public static DateTime DateTimeFromEpochMs(long unixTimestamp)
        {
            long ticks = unixTimestamp * TimeSpan.TicksPerMillisecond;
            ticks = ticks + new DateTime(1970, 1, 1).Ticks;
            return new DateTime(ticks);
        }


        private void ReadFromQueue(object o)
        {
            Thread.BeginThreadAffinity(); // ensure we are not switching between OS threads. Required at least for v8.

            while (!_stop)
            {
                TimeoutRequestNode msg = null;
                try
                {
                    var epochMs = EpochMsFromDateTime(DateTime.UtcNow);
                    lock (_queueLock)
                        _queue.TryPeek(out msg);
                    if (msg == null)
                    {
                        _starving = true;
                        _msgAddEvent.Wait(100);
                        _msgAddEvent.Reset();
                        _starving = false;
                    }
                    else if (msg.TimeoutMs > epochMs)
                    {
                        _starving = true;
                        _msgAddEvent.Wait((int)(msg.TimeoutMs - epochMs));
                        _msgAddEvent.Reset();
                        _starving = false;
                    }
                    else       // epochMs >= msg.TimeoutMs
                    {
                        lock (_queueLock)
                            _queue.TryDequeue(out msg);
                        var overdueMs = (epochMs - msg.TimeoutMs);
                        if (overdueMs > 100)
                        {
                            //if(Log.LogLevel >= LogLevel.Info)
                            Log.LogInformation("Message should have timed out at " + msg.TimeoutMs + ", but it's now " + epochMs + " which is " + overdueMs + " late");
                        }
                        msg.SendTimeout();
                    }
                }
                catch (Exception ex)
                {
                    Log.LogError(ex, $"Error while processing message {msg} in queued handler TimeoutService.");
                }
            }
            _stopped.Set();
            Thread.EndThreadAffinity();
        }
        public void PostTimeoutAfter(
                            ICorrelatedMessage source,
                            Guid targetId,
                            long timeoutMs,
                            Action<TimeoutMessage> timeoutAction)
        {
            Ensure.NotNull(timeoutAction, "timeoutAction");
            var timeoutRequest = new TimeoutRequestNode(
                                                 source,
                                                 targetId,
                                                 timeoutMs,
                                                 timeoutAction);

            lock (_queueLock)
            {
                if (_queue.Count >= _queue.MaxSize)
                    _queue.Resize(_queue.MaxSize + 100);
                _queue.Enqueue(timeoutRequest, timeoutMs);
            }
            if (_starving)
                _msgAddEvent.Set();
        }
    }
}
