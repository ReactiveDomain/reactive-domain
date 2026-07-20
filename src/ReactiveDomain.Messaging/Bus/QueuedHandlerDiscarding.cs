using System.Collections.Concurrent;
using ReactiveDomain.Logging;
using ReactiveDomain.Messaging.Monitoring.Stats;
using ReactiveDomain.Util;

namespace ReactiveDomain.Messaging.Bus;

/// <summary>
/// Lightweight in-memory queue with a separate thread in which it passes messages
/// to the consumer. It also tracks statistics about the message processing to help
/// in identifying bottlenecks
/// </summary>
/// 

// ReSharper disable RedundantExtendsListEntry
public class QueuedHandlerDiscarding : IQueuedHandler, IHandle<IMessage>, IPublisher, IMonitoredQueue, IThreadSafePublisher {
	private static readonly ILogger _log = LogManager.GetLogger("ReactiveDomain");

	public int MessageCount { get { return _queue.Count; } }
	public string Name { get { return _queueStats.Name; } }
	public bool Idle => _starving;
	private readonly IHandle<IMessage> _consumer;

	private readonly bool _watchSlowMsg;
	private readonly TimeSpan _slowMsgThreshold;

	private readonly ConcurrentQueue<IMessage> _queue = new();
	private readonly ManualResetEventSlim _msgAddEvent = new(false);

	private Thread? _thread;
	private volatile bool _stop;
	private volatile bool _starving;
	private readonly ManualResetEventSlim _stopped = new(true);
	private readonly TimeSpan _threadStopWaitTimeout;
	private readonly TimeSpan _idlePollInterval;

	private readonly QueueMonitor _queueMonitor;
	private readonly QueueStatsCollector _queueStats;

	public QueuedHandlerDiscarding(
		IHandle<IMessage> consumer,
		string name,
		bool watchSlowMsg = true,
		TimeSpan? slowMsgThreshold = null,
		TimeSpan? threadStopWaitTimeout = null,
		string? groupName = null,
		TimeSpan? idlePollInterval = null) {
		Ensure.NotNull(consumer, "consumer");
		Ensure.NotNull(name, "name");

		_consumer = consumer;

		_watchSlowMsg = watchSlowMsg;
		_slowMsgThreshold = slowMsgThreshold ?? InMemoryBus.DefaultSlowMessageThreshold;
		_threadStopWaitTimeout = threadStopWaitTimeout ?? QueuedHandler.DefaultStopWaitTimeout;
		_idlePollInterval = idlePollInterval ?? TimeSpan.FromMilliseconds(100);

		_queueMonitor = QueueMonitor.Default;
		_queueStats = new QueueStatsCollector(name, groupName);
	}

	public void Start() {
		if (_thread != null)
			throw new InvalidOperationException("Already a thread running.");

		_queueMonitor.Register(this);

		_stopped.Reset();

		_thread = new Thread(ReadFromQueue) { IsBackground = true, Name = Name };
		_thread.Start();
	}

	public void Stop() {
		_stop = true;
		// Wake an idle pump parked in its poll wait so it observes _stop immediately.
		_msgAddEvent.Set();
		if (!_stopped.Wait(_threadStopWaitTimeout))
			throw new TimeoutException($"Unable to stop thread '{Name}'.");
	}

	public void RequestStop() {
		_stop = true;
		// Wake an idle pump parked in its poll wait so it observes _stop immediately.
		_msgAddEvent.Set();
	}

	private void ReadFromQueue(object? o) {
		_queueStats.Start();
		Thread.BeginThreadAffinity(); // ensure we are not switching between OS threads. Required at least for v8.

		while (!_stop) {
			IMessage? msg = null;
			try {
				if (!_queue.TryDequeue(out msg)) {
					_starving = true;

					_queueStats.EnterIdle();
					_msgAddEvent.Wait(_idlePollInterval);
					_msgAddEvent.Reset();

					_starving = false;
				} else {
					while (_queue.TryDequeue(out var deadMessage)) {
						msg = deadMessage;
						_log.Debug("Discarding Messages");
					}

					_queueStats.EnterBusy();

					var cnt = _queue.Count;
					_queueStats.ProcessingStarted(msg.GetType(), cnt);

					if (_watchSlowMsg) {
						var start = DateTime.UtcNow;

						_consumer.Handle(msg);

						var elapsed = DateTime.UtcNow - start;
						if (elapsed > _slowMsgThreshold) {
							_log.Trace("SLOW QUEUE MSG [{0}]: {1} - {2}ms. Q: {3}/{4}.",
								Name, _queueStats.InProgressMessage!.Name, (int)elapsed.TotalMilliseconds, cnt, _queue.Count);
							if (elapsed > QueuedHandler.VerySlowMsgThreshold)// && !(msg is SystemMessage.SystemInit))
								_log.Error("---!!! VERY SLOW QUEUE MSG [{0}]: {1} - {2}ms. Q: {3}/{4}.",
									Name, _queueStats.InProgressMessage.Name, (int)elapsed.TotalMilliseconds, cnt, _queue.Count);
						}
					} else {
						_consumer.Handle(msg);
					}

					_queueStats.ProcessingEnded(1);
				}
			} catch (Exception ex) {
				_log.ErrorException(ex, $"Error while processing message {msg} in queued handler '{Name}'.");
			}
		}
		_queueStats.Stop();

		_stopped.Set();
		_queueMonitor.Unregister(this);
		Thread.EndThreadAffinity();
	}

	public void Publish(IMessage message) {
		//Ensure.NotNull(message, "message");
		_queue.Enqueue(message);
		if (_starving)
			_msgAddEvent.Set();
	}

	public void Handle(IMessage message) {
		Publish(message);
	}

	public QueueStats GetStatistics() {
		return _queueStats.GetStatistics(_queue.Count);
	}
}
