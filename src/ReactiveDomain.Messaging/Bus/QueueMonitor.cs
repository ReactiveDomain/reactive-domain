using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

namespace ReactiveDomain.Messaging.Bus
{
    public class QueueMonitor
    {
        private static readonly ILogger Log = Logging.LogProvider.GetLogger("ReactiveDomain");
        public static readonly QueueMonitor Default = new QueueMonitor();

        private readonly ConcurrentDictionary<IMonitoredQueue, IMonitoredQueue> _queues = new ConcurrentDictionary<IMonitoredQueue, IMonitoredQueue>();

        private QueueMonitor()
        {
        }

        public void Register(IMonitoredQueue monitoredQueue)
        {
            _queues[monitoredQueue] = monitoredQueue;
        }

        public void Unregister(IMonitoredQueue monitoredQueue)
        {
            IMonitoredQueue v;
            _queues.TryRemove(monitoredQueue, out v);
        }
    }
}