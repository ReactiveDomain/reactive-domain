using System;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Messaging.Util;

namespace ReactiveDomain.Foundation.EventStore
{
    public class ReadModelBase:
        IListener
    {
    
        private readonly IListener _listener;

        public ReadModelBase(Func<IListener> getListener)
        {
            Ensure.NotNull(getListener, nameof(getListener));
            _listener = getListener();
        }
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        private bool _disposed;
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            if (disposing)
            {
                _listener?.Dispose();
            }
            _disposed = true;
        }

        #region Implementation of IListener

        public ISubscriber EventStream => _listener.EventStream;

        public void Start(string stream, int? checkpoint = null, bool blockUntilLive = false, int millisecondsTimeout = 1000)
        {
            _listener.Start(stream, checkpoint, blockUntilLive, millisecondsTimeout);
        }

        public void Start<TAggregate>(Guid id, int? checkpoint = null, bool blockUntilLive = false, int millisecondsTimeout = 1000) where TAggregate : class, IEventSource
        {
            _listener.Start<TAggregate>(id, checkpoint, blockUntilLive, millisecondsTimeout);
        }

        public void Start<TAggregate>(int? checkpoint = null, bool blockUntilLive = false, int millisecondsTimeout = 1000) where TAggregate : class, IEventSource
        {
            _listener.Start<TAggregate>(checkpoint, blockUntilLive, millisecondsTimeout);
        }

        #endregion
    }
}
