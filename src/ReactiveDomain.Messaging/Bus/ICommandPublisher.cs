using System;

namespace ReactiveDomain.Messaging.Bus
{
    public interface ICommandPublisher
    {
        void Fire(Command command, string exceptionMsg =null, TimeSpan? responseTimeout = null,TimeSpan? ackTimeout = null);
        bool TryFire(Command command, out CommandResponse response, TimeSpan? responseTimeout = null,TimeSpan? ackTimeout = null);
        bool TryFire(Command command, TimeSpan? responseTimeout = null,TimeSpan? ackTimeout = null);
    }
}
