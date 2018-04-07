using System;

namespace ReactiveDomain.Messaging.Bus
{
    public interface IDispatcher : ICommandBus, IBus, IDisposable
    {
        bool Idle {get;}
    }
}
