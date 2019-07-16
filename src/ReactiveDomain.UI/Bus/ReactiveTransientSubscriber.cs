using System;
using System.Collections.Generic;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveUI;

// ReSharper disable RedundantTypeArgumentsOfMethod

namespace ReactiveDomain.UI
{
    public abstract class ReactiveTransientSubscriber : ReactiveObject, IDisposable
    {
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private readonly ISubscriber _eventSubscriber;
        private readonly ICommandSubscriber _commandSubscriber;

        protected ReactiveTransientSubscriber(IDispatcher bus) : this((IBus)bus)
        {
            _commandSubscriber = bus ?? throw new ArgumentNullException(nameof(bus));
        }

        protected ReactiveTransientSubscriber(IBus bus) : this((ISubscriber) bus) {}

        protected ReactiveTransientSubscriber(ISubscriber subscriber)
        {
            _eventSubscriber = subscriber ?? throw new ArgumentNullException(nameof(subscriber));
        }

        protected void Subscribe<T>(IHandle<T> handler) where T : class, IMessage
        {
            _subscriptions.Add(_eventSubscriber.Subscribe<T>(handler));
        }

        protected void Subscribe<T>(IHandleCommand<T> handler) where T : Command
        {
            if (_commandSubscriber == null) throw new ArgumentOutOfRangeException(nameof(handler), @"TransientSubscriber not created with CommandBus to register on.");
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
            if (disposing){
                _subscriptions?.ForEach(s => s.Dispose());
            }
           _disposed = true;
        }
    }
}

