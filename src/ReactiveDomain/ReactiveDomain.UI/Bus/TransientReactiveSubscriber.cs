using System;
using System.Collections.Generic;
using ReactiveDomain.Bus;
using ReactiveDomain.Messaging;
using ReactiveUI;

// ReSharper disable RedundantTypeArgumentsOfMethod

namespace ReactiveDomain.UI.Bus
{
    public abstract class TransientReactiveSubscriber : ReactiveObject, IDisposable
    {
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private readonly ISubscriber _eventSubscriber;
        private readonly ICommandSubscriber _commandSubscriber;

        protected TransientReactiveSubscriber(IGeneralBus bus) : this((IBus)bus)
        {
            _commandSubscriber = bus ?? throw new ArgumentNullException(nameof(bus));
        }

        protected TransientReactiveSubscriber(IBus bus) : this((ISubscriber)bus)
        {
            if (bus == null) throw new ArgumentNullException(nameof(bus));
        }

        protected TransientReactiveSubscriber(ISubscriber subscriber)
        {
            _eventSubscriber = subscriber ?? throw new ArgumentNullException(nameof(subscriber));
        }

        protected void Subscribe<T>(IHandle<T> handler) where T : Message
        {
            _subscriptions.Add(_eventSubscriber.Subscribe<T>(handler));
        }

        protected void Subscribe<T>(IHandleCommand<T> handler) where T : Command
        {
            if (_commandSubscriber == null) throw new ArgumentOutOfRangeException(nameof(handler), @"TransientReactiveSubscriber not created with a CommandBus to register on.");
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
        }
    }
}
