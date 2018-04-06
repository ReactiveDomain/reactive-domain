using System;
using System.Collections.Concurrent;
using System.Threading;
using ReactiveDomain.Logging;
using ReactiveDomain.Util;

namespace ReactiveDomain.Messaging.Bus
{
    /// <summary>
    /// Lightweight in-memory queue with a separate thread in which it passes messages
    /// to the consumer. It also tracks statistics about the message processing to help
    /// in identifying bottlenecks
    /// </summary>
    public class QueuedHandlerThreadPool : IQueuedHandler, IMonitoredQueue, IThreadSafePublisher
    {
        private static readonly ILogger Log = LogManager.GetLogger("ReactiveDomain");

        public int MessageCount => _queue.Count;
        public string Name => _queueStats.Name;
        public bool Idle => _starving;
        private readonly IHandle<Message> _consumer;

        private readonly bool _watchSlowMsg;
        private readonly TimeSpan _slowMsgThreshold;

        private readonly ConcurrentQueue<Message> _queue = new ConcurrentQueue<Message>();

        private volatile bool _stop;
        private readonly ManualResetEventSlim _stopped = new ManualResetEventSlim(true);
        private readonly TimeSpan _threadStopWaitTimeout;

        // monitoring
        private readonly QueueMonitor _queueMonitor;
        private readonly QueueStatsCollector _queueStats;

        private int _isRunning;
        private volatile bool _starving;

        public QueuedHandlerThreadPool(IHandle<Message> consumer,
                                       string name,
                                       bool watchSlowMsg = true,
                                       TimeSpan? slowMsgThreshold = null,
                                       TimeSpan? threadStopWaitTimeout = null,
                                       string groupName = null)
        {
            Ensure.NotNull(consumer, "consumer");
            Ensure.NotNull(name, "name");

            _consumer = consumer;

            _watchSlowMsg = watchSlowMsg;
            _slowMsgThreshold = slowMsgThreshold ?? InMemoryBus.DefaultSlowMessageThreshold;
            _threadStopWaitTimeout = threadStopWaitTimeout ?? QueuedHandler.DefaultStopWaitTimeout;

            _queueMonitor = QueueMonitor.Default;
            _queueStats = new QueueStatsCollector(name, groupName);
        }

        public void Start()
        {
            _queueStats.Start();
            _queueMonitor.Register(this);
        }

        public void Stop()
        {
            _stop = true;
            if (!_stopped.Wait(_threadStopWaitTimeout))
                throw new TimeoutException($"Unable to stop thread '{Name}'.");
            _queueMonitor.Unregister(this);
        }

        public void RequestStop()
        {
            _stop = true;
            _queueMonitor.Unregister(this);
        }

        private void ReadFromQueue(object o)
        {
            bool proceed = true;
            while (proceed)
            {
                _stopped.Reset();
                _starving = false;
                _queueStats.EnterBusy();

                Message msg;
                while (!_stop && _queue.TryDequeue(out msg))
                {
                    try
                    {
                        var queueCnt = _queue.Count;
                        _queueStats.ProcessingStarted(msg.GetType(), queueCnt);

                        if (_watchSlowMsg)
                        {
                            var start = DateTime.UtcNow;

                            _consumer.Handle(msg);

                            var elapsed = DateTime.UtcNow - start;
                            if (elapsed > _slowMsgThreshold)
                            {
                                Log.Trace("SLOW QUEUE MSG [{0}]: {1} - {2}ms. Q: {3}/{4}.",
                                          _queueStats.Name, _queueStats.InProgressMessage.Name, (int)elapsed.TotalMilliseconds, queueCnt, _queue.Count);
                                if (elapsed > QueuedHandler.VerySlowMsgThreshold)// && !(msg is SystemMessage.SystemInit))
                                    Log.Error("---!!! VERY SLOW QUEUE MSG [{0}]: {1} - {2}ms. Q: {3}/{4}.",
                                              _queueStats.Name, _queueStats.InProgressMessage.Name, (int)elapsed.TotalMilliseconds, queueCnt, _queue.Count);
                            }
                        }
                        else
                        {
                            _consumer.Handle(msg);
                        }

                        _queueStats.ProcessingEnded(1);
                    }
                    catch (Exception ex)
                    {
                        Log.ErrorException(ex,
                            $"Error while processing message {msg} in queued handler '{_queueStats.Name}'.");
                    }
                }
                _starving = true;
                _queueStats.EnterIdle();
                _stopped.Set();

                Interlocked.CompareExchange(ref _isRunning, 0, 1);
                // try to reacquire lock if needed
                proceed = !_stop && _queue.Count > 0 && Interlocked.CompareExchange(ref _isRunning, 1, 0) == 0; 
            }
        }

        public void Publish(Message message)
        {
            //Ensure.NotNull(message, "message");
            _queue.Enqueue(message);
            if (Interlocked.CompareExchange(ref _isRunning, 1, 0) == 0)
                ThreadPool.QueueUserWorkItem(ReadFromQueue);
        }

        public void Handle(Message message)
        {
            Publish(message);
        }

        //public QueueStats GetStatistics()
        //{
        //    return _queueStats.GetStatistics(_queue.Count);
        //}
    }
}

