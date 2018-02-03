using System;
using System.Collections.Generic;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveUI;

// ReSharper disable RedundantTypeArgumentsOfMethod

namespace ReactiveDomain.Foundation
{
    public abstract class TransientSubscriber : ReactiveObject, IDisposable
    {
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private readonly ISubscriber _eventSubscriber;
        private readonly ICommandSubscriber _commandSubscriber;

        protected TransientSubscriber(IGeneralBus bus) : this((IBus)bus)
        {
            if (bus == null) throw new ArgumentNullException(nameof(bus));
            _commandSubscriber = bus;
        }

        protected TransientSubscriber(IBus bus) : this((ISubscriber)bus)
        {
            if (bus == null) throw new ArgumentNullException(nameof(bus));
        }

        protected TransientSubscriber(ISubscriber subscriber)
        {
            if (subscriber == null) throw new ArgumentNullException(nameof(subscriber));
            _eventSubscriber = subscriber;
        }

        protected void Subscribe<T>(IHandle<T> handler) where T : Message
        {
            _subscriptions.Add(_eventSubscriber.Subscribe<T>(handler));
        }

        protected void Subscribe<T>(IHandleCommand<T> handler) where T : Command
        {
            if (_commandSubscriber == null) throw new ArgumentOutOfRangeException(nameof(handler), @"TransientSubscrier not created with CommandBus to register on.");
            _subscriptions.Add(_commandSubscriber.Subscribe<T>(handler));
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
                _subscriptions?.ForEach(s => s.Dispose());
                _disposed = true;
            }
            //_disposed = true;- moved back.
        }
    }
}

