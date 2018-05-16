using System;

namespace ReactiveDomain.Messaging.Bus
{
    /// <inheritdoc cref="ICommandBus"/>
    /// <inheritdoc cref="IBus"/>
    public interface IDispatcher : ICommandBus, IBus, IDisposable
    {
        bool Idle {get;}
    }
}
