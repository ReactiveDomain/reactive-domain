using System;

namespace ReactiveDomain.Messaging.Messages
{
    public interface IEvent
    {
        Guid MsgId { get; }
    }
}
