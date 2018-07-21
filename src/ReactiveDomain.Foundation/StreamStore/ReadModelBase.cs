using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveDomain.Messaging.Bus;
using ReactiveDomain.Util;

// ReSharper disable once CheckNamespace
namespace ReactiveDomain.Foundation {
    public abstract class ReadModelBase:IDisposable {
        private readonly Func<IListener> _getListener;
        private readonly List<IListener> _listeners;
        private readonly InMemoryBus _bus;
        private readonly QueuedHandler _queue;
        
        protected ReadModelBase(string name, Func<IListener> getListener) {
            
            Ensure.NotNull(getListener, nameof(getListener));
            _getListener = getListener;
            _listeners = new List<IListener>();
            _bus = new InMemoryBus($"{nameof(ReadModelBase)}:{name} bus",false);
            _queue = new QueuedHandler(_bus,$"{nameof(ReadModelBase)}:{name} queue");
            _queue.Start();
        }
       private IListener AddNewListener() {
           var l = _getListener();
           lock (_listeners) {
               _listeners.Add(l);
           }
           l.EventStream.Subscribe(_queue);
           return l;
       }
        public List<Tuple<string, long>> GetCheckpoint() {
            lock (_listeners) {
                return _listeners.Select(l => new Tuple<string, long>(l.StreamName, l.Position)).ToList();
            }
        }

        public ISubscriber EventStream => _bus;

        public void Start(string stream, long? checkpoint = null, bool blockUntilLive = false, int millisecondsTimeout = 1000) {
           
            AddNewListener().Start(stream, checkpoint, blockUntilLive, millisecondsTimeout);
        }

        public void Start<TAggregate>(Guid id, long? checkpoint = null, bool blockUntilLive = false, int millisecondsTimeout = 1000) where TAggregate : class, IEventSource {
            AddNewListener().Start<TAggregate>(id, checkpoint, blockUntilLive, millisecondsTimeout);
        }

        public void Start<TAggregate>(long? checkpoint = null, bool blockUntilLive = false, int millisecondsTimeout = 1000) where TAggregate : class, IEventSource {
            AddNewListener().Start<TAggregate>(checkpoint, blockUntilLive, millisecondsTimeout);
        }

        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        private bool _disposed;
        protected virtual void Dispose(bool disposing) {
            if (_disposed) return;
            if (disposing) {
                lock (_listeners) {
                    _listeners?.ForEach(l => l?.Dispose());
                }
                _queue?.RequestStop();
                _bus?.Dispose();
            }
            _disposed = true;
        }
    }
}
