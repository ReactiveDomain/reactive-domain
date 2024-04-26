﻿using System;
using ReactiveDomain.Messaging;
using ReactiveDomain.Messaging.Bus;

namespace ReactiveDomain.Testing
{
    public abstract class DispatcherSpecification : IDisposable
    {
        public readonly IDispatcher Dispatcher;
        public readonly IDispatcher LocalBus;
        public readonly TestQueue TestQueue;

        protected DispatcherSpecification()
        {
            Dispatcher = new Dispatcher("Fixture Bus", slowMsgThreshold: TimeSpan.FromMilliseconds(500));
            LocalBus = new Dispatcher("Fixture LocalBus");
            TestQueue = new TestQueue(Dispatcher, new[] { typeof(Event), typeof(Command) });
        }

        private bool _disposed;
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;
            if (disposing)
            {
                Dispatcher?.Dispose();
                LocalBus?.Dispose();
                TestQueue?.Dispose();
            }
            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
