using System;

namespace ReactiveDomain.Messaging.Bus
{
    public interface ICommandPublisher
    {
        void Send(Command command, string exceptionMsg =null, TimeSpan? responseTimeout = null,TimeSpan? ackTimeout = null);
        bool TrySend(Command command, out CommandResponse response, TimeSpan? responseTimeout = null,TimeSpan? ackTimeout = null);
        bool TrySend(Command command, TimeSpan? responseTimeout = null,TimeSpan? ackTimeout = null);
    }
}
