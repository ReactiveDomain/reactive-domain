using System;
using System.Collections.Generic;
using ReactiveDomain.Messaging;

namespace ReactiveDomain.Bus
{
    public abstract class QueuedSubscriber :
                            IHandle<Message>,
                            IDisposable
    {
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private readonly QueuedHandler _messageQueue;
        private readonly IGeneralBus _generalBus;
        private readonly IGeneralBus _internalBus;
        protected object Last = null;

        public abstract void HandleDynamic(dynamic message);
        protected QueuedSubscriber(IGeneralBus bus)
        {
            if (bus == null) throw new ArgumentNullException(nameof(bus));
            _generalBus = bus;
            _internalBus = new CommandBus("SubscriptionBus");
            _messageQueue = new QueuedHandler(this, "SubscriptionQueue");
            _messageQueue.Start();
        }

        public void Subscribe<T>(IHandle<T> handler) where T : Message
        {
            _internalBus.Subscribe<T>(handler);

            Action<T> publish = _messageQueue.Publish;
            var adHocHandler = new AdHocHandler<T>(publish);
            _subscriptions.Add(_generalBus.Subscribe<T>(adHocHandler));
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        private bool _disposed;
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            if (disposing)
            {
                _messageQueue.RequestStop();
                _subscriptions?.ForEach(s => s.Dispose());
                _disposed = true;
            }
        }

        public virtual void Handle(Message message)
        {
            if (Last == message) return;

            Last = message;
            dynamic msg = message;
            HandleDynamic(msg);
        }
    }
}

