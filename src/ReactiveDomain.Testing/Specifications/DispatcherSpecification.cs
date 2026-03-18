using System;
using System.Collections.Generic;
using System.Linq;
using ReactiveDomain.Messaging;
using ReactiveDomain.Testing.Messaging;

namespace ReactiveDomain.Testing;

public abstract class DispatcherSpecification : IDisposable {
    public readonly CapturingSubscribableBus Dispatcher = new();
    public readonly CapturingSubscribableBus LocalBus = new();

    /// <summary>
    /// Gets a snapshot of the list of messages that have been published or sent on the <see cref="Dispatcher"/>,
    /// in the order they were placed on the bus.
    /// </summary>
    /// <remarks>This is actually a List, not a Queue, but the name is preserved for backward compatibility.</remarks>
    public List<IMessage> TestQueue => Dispatcher.AllMessages.ToList();

    public virtual void ClearQueues() {
        Dispatcher.ClearMessages();
    }

    private bool _disposed;
    protected virtual void Dispose(bool disposing) {
        if (_disposed)
            return;
        if (disposing) {
            Dispatcher?.Dispose();
            LocalBus?.Dispose();
        }
        _disposed = true;
    }

    public void Dispose() {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}