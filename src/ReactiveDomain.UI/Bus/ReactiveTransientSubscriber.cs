using System;
using System.Collections.Generic;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;
using ReactiveUI;

// ReSharper disable RedundantTypeArgumentsOfMethod

namespace ReactiveDomain.UI
{
    /// <summary>
    /// A <see cref="ReactiveObject"/> that implements Subscribe on the injected bus and
    /// that auto-unsubscribes when disposed.
    /// </summary>
    public abstract class ReactiveTransientSubscriber : ReactiveObject, IDisposable
    {
        private readonly List<IDisposable> _subscriptions = new List<IDisposable>();
        private readonly ISubscriber _eventSubscriber;
        private readonly ICommandSubscriber _commandSubscriber;

        /// <summary>
        /// Creates a <see cref="ReactiveTransientSubscriber"/> with the injected dispatcher.
        /// </summary>
        /// <param name="bus">The dispatcher on which to subscribe to commands.</param>
        /// <exception cref="ArgumentNullException"><c>bus</c> is <c>null</c></exception>
        protected ReactiveTransientSubscriber(IDispatcher bus) : this((IBus)bus)
        {
            _commandSubscriber = bus ?? throw new ArgumentNullException(nameof(bus));
        }

        /// <summary>
        /// Creates a <see cref="ReactiveTransientSubscriber"/> with the injected bus.
        /// </summary>
        /// <param name="bus">The bus on which to subscribe to messages.</param>
        /// <exception cref="ArgumentNullException"><c>bus</c> is <c>null</c></exception>
        protected ReactiveTransientSubscriber(IBus bus) : this((ISubscriber) bus) { }

        /// <summary>
        /// Creates a <see cref="ReactiveTransientSubscriber"/> with the injected bus.
        /// </summary>
        /// <param name="subscriber">The <see cref="ISubscriber"/> on which to subscribe to messages.</param>
        /// <exception cref="ArgumentNullException"><c>bus</c> is <c>null</c></exception>
        protected ReactiveTransientSubscriber(ISubscriber subscriber)
        {
            _eventSubscriber = subscriber ?? throw new ArgumentNullException(nameof(subscriber));
        }

        /// <summary>
        /// Subscribes to messages of type T on the bus. This is typically used when subscribing to events.
        /// </summary>
        /// <typeparam name="T">The type of messages to subscribe to.</typeparam>
        /// <param name="handler">A handler for messages of type T.</param>
        protected void Subscribe<T>(IHandle<T> handler) where T : class, IMessage
        {
            _subscriptions.Add(_eventSubscriber.Subscribe<T>(handler));
        }

        /// <summary>
        /// Subscribes to commands of type T on the bus.
        /// </summary>
        /// <typeparam name="T">The type of commands to subscribe to.</typeparam>
        /// <param name="handler">A handler for commands of type T.</param>
        /// <exception cref="ArgumentOutOfRangeException">The class has not been given an
        /// <see cref="IDispatcher"/> on which to subscribe to commands.</exception>
        protected void Subscribe<T>(IHandleCommand<T> handler) where T : Command
        {
            if (_commandSubscriber == null) throw new ArgumentOutOfRangeException(nameof(handler), @"TransientSubscriber not created with CommandBus to register on.");
            _subscriptions.Add(_commandSubscriber.Subscribe<T>(handler));
        }

        /// <summary>
        /// Unsubscribes from all subscribed message types on the bus.
        /// </summary>
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
            if (disposing) {
                _subscriptions?.ForEach(s => s.Dispose());
            }
            _disposed = true;
        }
    }
}

