using System;
using System.Collections.Generic;
using ReactiveDomain.Util;

namespace ReactiveDomain.Messaging.Bus {
    public abstract class QueuedSubscriber :
                            IDisposable {

        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();

        private readonly QueuedHandler _messageQueue;
        private readonly IBus _externalBus;
        private readonly InMemoryBus _internalBus;
        protected object Last = null;
        public bool Starving => _messageQueue.Idle;
        protected QueuedSubscriber(IBus bus, bool idempotent = false) {
            _externalBus = bus ?? throw new ArgumentNullException(nameof(bus));
            _internalBus = new InMemoryBus("SubscriptionBus");

            if (idempotent)
                _messageQueue = new QueuedHandler(
                                        new IdempotentHandler<IMessage>(
                                                new AdHocHandler<IMessage>(_internalBus.Publish)
                                                ),
                                        "SubscriptionQueue");
            else
                _messageQueue = new QueuedHandler(
                                        new AdHocHandler<IMessage>(_internalBus.Publish),
                                        "SubscriptionQueue");
            _messageQueue.Start();
        }
        public IDisposable Subscribe<T>(IHandle<T> handler) where T : class,IMessage {
            var internalSub = _internalBus.Subscribe(handler);
            var externalSub = _externalBus.Subscribe(new AdHocHandler<T>(_messageQueue.Handle));
            _subscriptions.Add(internalSub);
            _subscriptions.Add(externalSub);
            return new Disposer(() => {
                internalSub?.Dispose();
                externalSub?.Dispose();
                return Unit.Default;
            });
        }
        public IDisposable Subscribe<T>(IHandleCommand<T> handler) where T : class,ICommand {
            var internalSub = _internalBus.Subscribe(new CommandHandler<T>(_externalBus, handler));
            var externalSub = _externalBus.Subscribe(new AdHocHandler<T>(_messageQueue.Handle));
            _subscriptions.Add(internalSub);
            _subscriptions.Add(externalSub);
            return new Disposer(() => {
                internalSub?.Dispose();
                externalSub?.Dispose();
                return Unit.Default;
            });
        }
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        private bool _disposed;
        protected virtual void Dispose(bool disposing) {
            if (_disposed)
                return;
            if (disposing) {
                _messageQueue.RequestStop();
                _subscriptions?.ForEach(s => s.Dispose());
                _internalBus?.Dispose();
                _disposed = true;
            }
        }
    }
}

