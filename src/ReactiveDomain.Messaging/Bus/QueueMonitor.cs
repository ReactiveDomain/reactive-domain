using System.Collections.Concurrent;

namespace ReactiveDomain.Messaging.Bus;

public class QueueMonitor {
	public static readonly QueueMonitor Default = new();

	private readonly ConcurrentDictionary<IMonitoredQueue, IMonitoredQueue> _queues = new();

	private QueueMonitor() { }

	public void Register(IMonitoredQueue monitoredQueue) {
		_queues[monitoredQueue] = monitoredQueue;
	}

	public void Unregister(IMonitoredQueue monitoredQueue) {
		_queues.TryRemove(monitoredQueue, out _);
	}
}
