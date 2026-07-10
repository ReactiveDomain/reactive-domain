using System.Diagnostics;
using ReactiveDomain.Messaging.Monitoring.Stats;
using ReactiveDomain.Util;

// ReSharper disable MemberCanBePrivate.Global

namespace ReactiveDomain.Messaging.Bus;

public class QueueStatsCollector {
	private static readonly TimeSpan _minRefreshPeriod = TimeSpan.FromMilliseconds(100);

	public readonly string Name;

	public readonly string? GroupName;

	public Type? InProgressMessage { get; private set; }

	private readonly object _statisticsLock = new(); // this lock is mostly acquired from a single thread (+ rarely to get statistics), so performance penalty is not too high

	private readonly Stopwatch _busyWatch = new();
	private readonly Stopwatch _idleWatch = new();
	private readonly Stopwatch _totalIdleWatch = new();
	private readonly Stopwatch _totalBusyWatch = new();
	private readonly Stopwatch _totalTimeWatch = new();
	private TimeSpan _lastTotalIdleTime;
	private TimeSpan _lastTotalBusyTime;
	private TimeSpan _lastTotalTime;

	private long _totalItems;
	private long _lastTotalItems;
	private int _lifetimeQueueLengthPeak;
	private int _currentQueueLengthPeak;
	private Type? _lastProcessedMsgType;

	private bool _wasIdle;

	public QueueStatsCollector(string name, string? groupName = null) {
		Ensure.NotNull(name, "name");

		Name = name;
		GroupName = groupName;
	}

	public void Start() {
		_totalTimeWatch.Start();
		EnterIdle();
	}

	public void Stop() {
		EnterIdle();
		_totalTimeWatch.Stop();
	}

	public void ProcessingStarted<T>(int queueLength) {
		ProcessingStarted(typeof(T), queueLength);
	}

	public void ProcessingStarted(Type msgType, int queueLength) {
		_lifetimeQueueLengthPeak = _lifetimeQueueLengthPeak > queueLength ? _lifetimeQueueLengthPeak : queueLength;
		_currentQueueLengthPeak = _currentQueueLengthPeak > queueLength ? _currentQueueLengthPeak : queueLength;

		InProgressMessage = msgType;
	}

	public void ProcessingEnded(int itemsProcessed) {
		Interlocked.Add(ref _totalItems, itemsProcessed);
		_lastProcessedMsgType = InProgressMessage;
		InProgressMessage = null;
	}

	public void EnterIdle() {
		if (_wasIdle)
			return;
		_wasIdle = true;

		//NOTE: the following locks are primarily acquired in main thread, 
		//      so not too high performance penalty
		lock (_statisticsLock) {
			_totalIdleWatch.Start();
			_idleWatch.Restart();

			_totalBusyWatch.Stop();
			_busyWatch.Reset();
		}
	}

	public void EnterBusy() {
		if (!_wasIdle)
			return;
		_wasIdle = false;

		lock (_statisticsLock) {
			_totalIdleWatch.Stop();
			_idleWatch.Reset();

			_totalBusyWatch.Start();
			_busyWatch.Restart();
		}
	}

	public QueueStats GetStatistics(int currentQueueLength) {
		lock (_statisticsLock) {
			var totalTime = _totalTimeWatch.Elapsed;
			var totalIdleTime = _totalIdleWatch.Elapsed;
			var totalBusyTime = _totalBusyWatch.Elapsed;
			var totalItems = Interlocked.Read(ref _totalItems);

			var lastRunMs = totalTime - _lastTotalTime;
			var lastItems = totalItems - _lastTotalItems;
			var avgItemsPerSecond = lastRunMs.Ticks != 0 ? (int)(TimeSpan.TicksPerSecond * lastItems / lastRunMs.Ticks) : 0;
			var avgProcessingTime = lastItems != 0 ? (totalBusyTime - _lastTotalBusyTime).TotalMilliseconds / lastItems : 0;
			var idleTimePercent = Math.Min(100.0, lastRunMs.Ticks != 0 ? 100.0 * (totalIdleTime - _lastTotalIdleTime).Ticks / lastRunMs.Ticks : 100);

			var stats = new QueueStats(
				Name,
				GroupName,
				currentQueueLength,
				avgItemsPerSecond,
				avgProcessingTime,
				idleTimePercent,
				_busyWatch.IsRunning ? _busyWatch.Elapsed : null,
				_idleWatch.IsRunning ? _idleWatch.Elapsed : null,
				totalItems,
				_currentQueueLengthPeak,
				_lifetimeQueueLengthPeak,
				_lastProcessedMsgType,
				InProgressMessage);

			if (totalTime - _lastTotalTime >= _minRefreshPeriod) {
				_lastTotalTime = totalTime;
				_lastTotalIdleTime = totalIdleTime;
				_lastTotalBusyTime = totalBusyTime;
				_lastTotalItems = totalItems;

				_currentQueueLengthPeak = 0;
			}
			return stats;
		}
	}
}
